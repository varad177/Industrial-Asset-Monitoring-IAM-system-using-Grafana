FROM node:22-alpine

WORKDIR /app

# Install dependencies first for caching
COPY package*.json ./
RUN npm install

# Copy source code
COPY . .

# Expose Vite's default port
EXPOSE 5173

# Run Vite in dev mode, binding to all interfaces
CMD ["npm", "run", "dev", "--", "--host", "0.0.0.0"]
