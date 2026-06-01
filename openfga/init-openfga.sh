#!/bin/sh

set -e

OPENFGA_URL="http://openfga:8080"
STORE_NAME="iam-monitor"

echo "⏳ Waiting for OpenFGA to become ready..."

until curl -sf "$OPENFGA_URL/healthz" > /dev/null; do
  echo "Waiting for OpenFGA..."
  sleep 2
done

echo "✅ OpenFGA is ready"

# ─────────────────────────────────────────────────────────────
# 1. Find Existing Store or Create New One
# ─────────────────────────────────────────────────────────────

echo "🔍 Checking if store already exists..."

EXISTING=$(curl -s "$OPENFGA_URL/stores")

STORE_ID=$(echo "$EXISTING" | \
  grep -o "\"id\":\"[^\"]*\",\"name\":\"$STORE_NAME\"" | \
  head -n 1 | \
  sed 's/"id":"\([^"]*\)","name":"[^"]*"/\1/')

if [ -n "$STORE_ID" ]; then
  echo "✅ Found existing store: $STORE_ID"

else
  echo "📦 Creating new store..."

  STORE_RESPONSE=$(curl -s -X POST "$OPENFGA_URL/stores" \
    -H "Content-Type: application/json" \
    -d "{\"name\":\"$STORE_NAME\"}")

  echo "Store Response: $STORE_RESPONSE"

  STORE_ID=$(echo "$STORE_RESPONSE" | \
    sed 's/.*"id":"\([^"]*\)".*/\1/')

  if [ -z "$STORE_ID" ]; then
    echo "❌ Failed to create store"
    exit 1
  fi

  echo "✅ Store created: $STORE_ID"
fi

# ─────────────────────────────────────────────────────────────
# 2. Upload Authorization Model (with team support)
# ─────────────────────────────────────────────────────────────

echo ""
echo "📋 Uploading authorization model..."

MODEL_RESPONSE=$(curl -s -X POST \
  "$OPENFGA_URL/stores/$STORE_ID/authorization-models" \
  -H "Content-Type: application/json" \
  -d @/scripts/model.json)

echo "Model Response: $MODEL_RESPONSE"

MODEL_ID=$(echo "$MODEL_RESPONSE" | \
  sed 's/.*"authorization_model_id":"\([^"]*\)".*/\1/')

if [ -z "$MODEL_ID" ]; then
  echo "❌ Failed to upload model"
  exit 1
fi

echo "✅ Model uploaded: $MODEL_ID"

# ─────────────────────────────────────────────────────────────
# 3. Seed Team Structure (one-time bootstrap)
#    All real user management is done via the Provisioning API.
# ─────────────────────────────────────────────────────────────

echo ""
echo "🌱 Seeding team structure tuples..."

WRITE_RESPONSE=$(curl -s -X POST \
  "$OPENFGA_URL/stores/$STORE_ID/write" \
  -H "Content-Type: application/json" \
  -d '{
    "writes": {
      "tuple_keys": [
        {
          "user": "user:admin@gmail.com",
          "relation": "member",
          "object": "team:admins"
        },
        {
          "user": "user:admin@localhost",
          "relation": "member",
          "object": "team:admins"
        },

        {
          "user": "team:admins#member",
          "relation": "viewer",
          "object": "asset:asset_1"
        },
        {
          "user": "team:admins#member",
          "relation": "viewer",
          "object": "asset:asset_2"
        },
        {
          "user": "team:admins#member",
          "relation": "viewer",
          "object": "asset:asset_3"
        },
        {
          "user": "team:admins#member",
          "relation": "viewer",
          "object": "asset:asset_4"
        },
        {
          "user": "team:admins#member",
          "relation": "viewer",
          "object": "dashboard:iam-asset-telemetry"
        },

        {
          "user": "team:operators#member",
          "relation": "viewer",
          "object": "asset:asset_1"
        },
        {
          "user": "team:operators#member",
          "relation": "viewer",
          "object": "asset:asset_2"
        },
        {
          "user": "team:operators#member",
          "relation": "viewer",
          "object": "dashboard:iam-asset-telemetry"
        },

        {
          "user": "team:viewers#member",
          "relation": "viewer",
          "object": "dashboard:iam-asset-telemetry"
        }
      ]
    }
  }' || true)

echo "Write Response: $WRITE_RESPONSE"

if echo "$WRITE_RESPONSE" | grep -q "already exists"; then
  echo "⚠️ Some tuples already exist — skipping duplicates"

elif echo "$WRITE_RESPONSE" | grep -q "\"code\""; then
  echo "⚠️ Non-fatal tuple response:"
  echo "$WRITE_RESPONSE"

else
  echo "✅ Team structure seeded successfully"
fi

# ─────────────────────────────────────────────────────────────
# Done
# ─────────────────────────────────────────────────────────────

echo ""
echo "🎉 OpenFGA initialization complete!"
echo "   Store ID : $STORE_ID"
echo "   Model ID : $MODEL_ID"
echo ""
echo "   Teams seeded: admins, operators, viewers"
echo "   Bootstrap admin: admin@gmail.com → team:admins"
echo "   Use the Provisioning API to manage users and teams."