# PetroTransit

PetroTransit is a full-stack reservation and scheduling application with:

- an ASP.NET Core API
- a React + Vite frontend
- PostgreSQL for persistence
- Docker-based deployment for the production app host

This README is the main entry point for the repo. Use it to understand the system layout, where code lives, and where to find the more detailed setup and operations docs.

## Architecture

Local development:

- PostgreSQL runs in Docker from the repo root `docker-compose.yml`
- the API runs with `dotnet run`
- the frontend runs with Vite

Production deployment:

- `EC2 #1`: OpenVPN host for admin access
- `EC2 #2`: app host running Docker for:
  - `caddy`
  - `web`
  - `api`
- `RDS PostgreSQL`: production database
- `Route53`: public DNS for:
  - `petrotransit.fonefit.com`
  - `apipetrotransit.fonefit.com`

Traffic flow:

1. Browser requests `https://petrotransit.fonefit.com`
2. Caddy on the app EC2 serves the frontend
3. Frontend API requests go to `https://apipetrotransit.fonefit.com`
4. Caddy proxies API traffic to the ASP.NET container on internal port `8080`
5. The API connects to PostgreSQL in RDS

Administrative access flow:

1. Admin connects to OpenVPN
2. Admin receives a VPN client IP in `10.8.0.0/24`
3. Admin SSHes to the app EC2 over its private IP
4. App EC2 SSH is not exposed to the public internet

## Folder Structure

Top-level layout:

- `PetroTransit.API/`
  - ASP.NET Core backend
  - controllers, DTOs, EF Core data access, models, migrations, and services
- `petro-transit-web/`
  - React frontend
  - pages, components, frontend services, Vite config, Dockerfile, and nginx static serving config
- `deploy/aws/`
  - production AWS deployment and operations docs
  - app host Docker deployment files
  - OpenVPN runbook
- `PetroTransit.API.Tests/`
  - backend test project
- `docker-compose.yml`
  - local development PostgreSQL setup
- `.env.example`
  - local development environment template
- `LOCALHOST-SETUP.txt`
  - quick local startup instructions

Backend layout highlights:

- `PetroTransit.API/Controllers/`
  - API endpoints
- `PetroTransit.API/Data/`
  - EF Core `AppDbContext`
- `PetroTransit.API/Models/`
  - domain and identity models
- `PetroTransit.API/DTOs/`
  - request and response DTOs
- `PetroTransit.API/services/`
  - email and SMTP-related services
- `PetroTransit.API/Migrations/`
  - EF Core migrations

Frontend layout highlights:

- `petro-transit-web/src/pages/`
  - route-level UI pages
- `petro-transit-web/src/components/`
  - shared UI components
- `petro-transit-web/src/Services/`
  - API client and frontend service helpers
- `petro-transit-web/public/`
  - static assets

AWS deployment layout:

- `deploy/aws/OPERATIONS.md`
  - complete setup, deployment, and day-2 operations guide
- `deploy/aws/README.md`
  - AWS architecture and deployment summary
- `deploy/aws/openvpn/README.md`
  - detailed OpenVPN EC2 runbook
- `deploy/aws/app/docker-compose.yml`
  - app EC2 Docker stack for `caddy + web + api`
- `deploy/aws/app/.env.example`
  - production app-host env template
- `deploy/aws/app/Caddyfile`
  - Caddy reverse proxy and TLS config

## Read This Next

If you are trying to:

- understand the project:
  - start here in `README.md`
- run locally:
  - read `LOCALHOST-SETUP.txt`
  - use `.env.example`
- work on the frontend:
  - read `petro-transit-web/README.md`
- work on backend tests:
  - read `PetroTransit.API.Tests/README.md`
- deploy or operate production:
  - start with `deploy/aws/OPERATIONS.md`
- set up admin VPN access:
  - read `deploy/aws/openvpn/README.md`

## Local Development Summary

Local development uses:

- Docker Desktop for PostgreSQL
- `dotnet run` for the API
- `npm run dev` for the frontend

Basic local startup:

```bash
cd PetroTransit
docker compose up -d db
```

In another shell:

```bash
cd PetroTransit/PetroTransit.API
dotnet run
```

In another shell:

```bash
cd PetroTransit/petro-transit-web
npm install
npm run dev
```

Frontend local URL:

- `http://localhost:5173`

API local URL:

- `http://localhost:5270`

## Production Summary

Production uses:

- OpenVPN on a dedicated EC2
- Docker on the app EC2
- Caddy for TLS termination and reverse proxying
- RDS PostgreSQL for the database
- SES for SMTP

Important production behavior:

- the frontend is built with `VITE_API_BASE_URL=https://apipetrotransit.fonefit.com`
- the API allows the frontend origin `https://petrotransit.fonefit.com`
- the app EC2 should expose only `80/443` publicly
- SSH to the app EC2 should be limited to the VPN client subnet

## Configuration Files

Common config files and what they are for:

- `.env.example`
  - local development env template
- `deploy/aws/app/.env.example`
  - production app-host env template
- `docker-compose.yml`
  - local PostgreSQL container
- `deploy/aws/app/docker-compose.yml`
  - production app stack
- `PetroTransit.API/Program.cs`
  - API startup, CORS, auth, migrations, and forwarded headers
- `petro-transit-web/src/Services/api.ts`
  - frontend API base URL handling

## Notes

- `PetroTransit/.env` is for local development and should remain untracked.
- `deploy/aws/app/.env` is for the production app EC2 and should remain untracked.
- Legacy AWS deployment folders were removed so `deploy/aws/app` is now the only supported app deployment path.
