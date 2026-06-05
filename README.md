# Industrial Asset Monitoring (IAM) System

An enterprise-grade Industrial Asset Monitoring platform built to demonstrate advanced identity, telemetry, and Relationship-Based Access Control (ReBAC). The system securely collects, aggregates, and visualizes IoT telemetry data for industrial assets while enforcing strict, fine-grained access policies using OpenFGA.

## 🌟 Architectural Diagram
<img width="1553" height="1013" alt="ChatGPT Image May 29, 2026, 10_49_18 AM" src="https://github.com/user-attachments/assets/5cafe1b3-1896-405e-9fd6-123d3b99bd3e" />


## 🌟 Key Features

* **Real-time Telemetry Visualization:** Embedded Grafana dashboards with customized, responsive UI integration via React.
* **Relationship-Based Access Control (ReBAC):** Fine-grained permission matrix using OpenFGA (Google Zanzibar model) to manage User → Team → Asset access dynamically.
* **Seamless Single Sign-On (SSO):** Custom `.NET` reverse proxy middleware that injects JWT assertions, allowing users to log into the React frontend and seamlessly view Grafana dashboards without secondary authentication.
* **OAuth2 Machine-to-Machine Auth:** Grafana securely fetches data from the backend API using the industry-standard OAuth2 Client Credentials flow.
* **Time-Series Data:** High-performance telemetry storage and aggregation using InfluxDB.

## 🏗️ Technology Stack

* **Backend API:** `.NET 8` (Clean Architecture, CQRS pattern, custom JWT authentication handler)
* **Frontend:** `React.js` (Vite, TailwindCSS, custom Glassmorphism UI)
* **Authorization Engine:** `OpenFGA` (ReBAC)
* **Telemetry Database:** `InfluxDB` (Time-series)
* **Visualization:** `Grafana` (embedded via iframe, Infinity Data Source)
* **Relational Database:** `SQL Server` (Entity Framework Core for Identity & User Management)
* **Infrastructure:** `Docker` & `Docker Compose`

## 📁 Project Structure

```text
├── frontend/             # React SPA (Vite)
├── IAM.API/              # .NET 8 Web API & Controllers
├── IAM.Application/      # CQRS Handlers, DTOs, Interfaces
├── IAM.Domain/           # Core Entities (User, Team, Asset)
├── IAM.Infrastructure/   # EF Core, InfluxDB Client, OpenFGA Client
├── grafana/              # Grafana provisioning (dashboards & Infinity OAuth2 data source)
├── openfga/              # OpenFGA Authorization Model (model.json) & Init scripts
└── docker-compose.yml    # Core infrastructure orchestration
```

## 🔐 Security Architecture

1. **User Authentication:** React logs in via the `.NET API`, receiving an `HttpOnly` cookie containing an RSA-signed JWT.
2. **Dashboard Rendering:** When the user loads a dashboard, the React app iframes Grafana. The `.NET API` intercepts the request via `GrafanaProxyMiddleware`, extracts the JWT, and forwards it to Grafana via an `X-JWT-Assertion` header to achieve SSO.
3. **Data Retrieval:** Grafana uses the Infinity Data Source to query the `.NET API`. It negotiates a machine-token using an **OAuth2 Client Credentials** handshake.
4. **Data Authorization:** The `.NET API` intercepts the data request and queries OpenFGA (`Does User X have Viewer access to Asset Y?`). If FGA returns true, the API queries InfluxDB and returns the JSON telemetry data.

## 🚀 Getting Started

### Prerequisites
* Docker & Docker Compose
* .NET 8 SDK
* Node.js (for frontend development)

### 1. Environment Setup
Create a `.env` file in the root directory. You can copy the provided `.env.example` file:
```bash
cp .env.example .env
```
Ensure `GRAFANA_CLIENT_SECRET` and `JWT_SECRET` are securely generated for production.

### 2. Run the Infrastructure
The entire platform (API, DBs, OpenFGA, Grafana) is containerized. Start the stack in detached mode:
```bash
docker-compose up -d --build
```

### 3. Access the Application
* **Frontend UI:** `http://localhost:5173`
* **Backend API (Swagger):** `http://localhost:5500/swagger`
* **Grafana (Internal):** `http://localhost:3000`

### 4. Default Admin Login
Upon first boot, the system seeds a default administrator account.
* **Email:** `admin@gmail.com`
* **Password:** `Admin@123`

---

## 🛠️ Development & Debugging

* **OpenFGA Playground:** Temporarily uncomment ports `8080` and `3102` in `docker-compose.yml` to access the visual FGA tuple builder.
* **Mock Data:** The InfluxDB container is pre-configured with continuous simulated telemetry data (temperatures, vibrations, pressure) for `asset_1` and `asset_2`.
* **Database Migrations:** Entity Framework Core migrations are automatically applied to the SQL Server container on API startup.

## 📝 License
This project is licensed under the MIT License.
