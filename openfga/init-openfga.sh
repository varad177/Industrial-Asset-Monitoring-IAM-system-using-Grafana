#!/bin/sh

OPENFGA_URL="http://openfga:8080"
STORE_ID_FILE="/data/store_id"
MODEL_ID_FILE="/data/model_id"
STORE_NAME="iam-monitor"

# ── 1. Get or Create Store ────────────────────────────────────────────────────
if [ -f "$STORE_ID_FILE" ]; then
  STORE_ID=$(cat "$STORE_ID_FILE")
  echo "✅ Using existing store: $STORE_ID"
else
  # Check if store exists in OpenFGA
  echo "🔍 Checking if store already exists in OpenFGA..."
  EXISTING=$(curl -sf "$OPENFGA_URL/stores")
  STORE_ID=$(echo "$EXISTING" | sed 's/.*"id":"\([^"]*\)","name":"iam-monitor".*/\1/')

  if [ -n "$STORE_ID" ] && [ "$STORE_ID" != "$EXISTING" ]; then
    echo "✅ Found existing store in OpenFGA: $STORE_ID"
    echo "$STORE_ID" > "$STORE_ID_FILE"
  else
    echo "📦 Creating new store..."
    STORE_RESPONSE=$(curl -sf -X POST "$OPENFGA_URL/stores" \
      -H "Content-Type: application/json" \
      -d '{"name":"iam-monitor"}')

    STORE_ID=$(echo "$STORE_RESPONSE" | sed 's/.*"id":"\([^"]*\)".*/\1/')

    if [ -z "$STORE_ID" ]; then
      echo "❌ Failed to create store. Response: $STORE_RESPONSE"
      exit 1
    fi

    echo "$STORE_ID" > "$STORE_ID_FILE"
    echo "✅ Store created: $STORE_ID"
  fi
fi

# ── 2. Always upload latest model (creates new version each time) ─────────────
echo ""
echo "📋 Uploading authorization model..."
MODEL_RESPONSE=$(curl -sf -X POST "$OPENFGA_URL/stores/$STORE_ID/authorization-models" \
  -H "Content-Type: application/json" \
  -d @/scripts/model.json)

MODEL_ID=$(echo "$MODEL_RESPONSE" | sed 's/.*"authorization_model_id":"\([^"]*\)".*/\1/')

if [ -z "$MODEL_ID" ]; then
  echo "❌ Failed to upload model. Response: $MODEL_RESPONSE"
  exit 1
fi

# Save latest model ID
echo "$MODEL_ID" > "$MODEL_ID_FILE"
echo "✅ Model uploaded: $MODEL_ID"

# ── 3. Seed tuples only on first run ─────────────────────────────────────────
TUPLES_SEEDED_FILE="/data/tuples_seeded"

if [ -f "$TUPLES_SEEDED_FILE" ]; then
  echo "✅ Tuples already seeded — skipping"
else
  echo ""
  echo "🌱 Seeding authorization tuples..."
  curl -sf -X POST "$OPENFGA_URL/stores/$STORE_ID/write" \
    -H "Content-Type: application/json" \
    -d "{
      \"writes\": {
        \"tuple_keys\": [
          { \"user\": \"user:admin\",           \"relation\": \"viewer\", \"object\": \"asset:asset_1\" },
          { \"user\": \"user:admin\",           \"relation\": \"viewer\", \"object\": \"asset:asset_2\" },
          { \"user\": \"user:admin\",           \"relation\": \"viewer\", \"object\": \"asset:asset_3\" },
          { \"user\": \"user:admin\",           \"relation\": \"viewer\", \"object\": \"asset:asset_4\" },
          { \"user\": \"user:admin\",           \"relation\": \"viewer\", \"object\": \"dashboard:iam-asset-telemetry\" },
          { \"user\": \"user:varad@gmail.com\", \"relation\": \"viewer\", \"object\": \"asset:asset_1\" },
          { \"user\": \"user:varad@gmail.com\", \"relation\": \"viewer\", \"object\": \"asset:asset_2\" },
          { \"user\": \"user:varad@gmail.com\", \"relation\": \"viewer\", \"object\": \"dashboard:iam-asset-telemetry\" },
          { \"user\": \"user:operator\",        \"relation\": \"viewer\", \"object\": \"asset:asset_3\" },
          { \"user\": \"user:operator\",        \"relation\": \"viewer\", \"object\": \"dashboard:iam-asset-telemetry\" }
        ]
      }
    }"

  touch "$TUPLES_SEEDED_FILE"
  echo "✅ Tuples seeded"
fi

echo ""
echo "🎉 OpenFGA initialisation complete!"
echo "   Store ID : $STORE_ID"
echo "   Model ID : $MODEL_ID"