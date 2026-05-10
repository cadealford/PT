# PetroTransit AWS Setup And Operations Guide

This guide is the end-to-end operator handbook for the production PetroTransit AWS environment.

Final production layout:

- `EC2 #1`: OpenVPN server installed directly on the host
- `EC2 #2`: app server running Docker for `postgres + caddy + web + api`
- `Route53`: DNS for:
  - `petrotransit.fonefit.com`
  - `apipetrotransit.fonefit.com`

Use this document when you need to:

- build the environment from scratch
- deploy a new version
- manage the servers day to day
- troubleshoot the most common production issues

## 1. What Each Piece Does

OpenVPN EC2:

- gives administrators a private access path into AWS
- exposes `1194/udp` publicly
- should not host the application

App EC2:

- hosts the frontend, API, and Caddy reverse proxy
- hosts PostgreSQL in Docker on the internal compose network
- exposes `80/tcp` and `443/tcp` publicly
- allows `22/tcp` only from the VPN client subnet

Route53:

- points both public hostnames to the app EC2 Elastic IP

SES SMTP:

- sends password reset and other outbound email

## 2. Initial AWS Build Order

Build the environment in this order:

1. Create the OpenVPN EC2 and security group.
2. Install and validate OpenVPN.
3. Create the app EC2 and security group.
4. Verify app EC2 SSH works only over VPN.
5. Create or confirm the SES SMTP credentials.
6. Clone the repo onto the app EC2.
7. Prepare `deploy/aws/app/.env`.
8. If needed, migrate existing RDS data into the Docker PostgreSQL container.
9. Deploy the Docker stack on the app EC2.
10. Point Route53 DNS to the app EC2 Elastic IP.
11. Validate frontend, API, login, and email.
12. Delete the old RDS instance after validation.

## 3. AWS Resources Checklist

OpenVPN EC2:

- Ubuntu 24.04
- `t4g.nano` if using an ARM AMI, otherwise use an x86 instance type such as `t3.nano`
- public subnet
- Elastic IP
- security group:
  - allow `1194/udp` from `0.0.0.0/0`
  - allow `22/tcp` only from your admin source IP

App EC2:

- Ubuntu 24.04
- enough CPU and RAM for Docker builds plus runtime
- public subnet
- Elastic IP
- security group:
  - allow `80/tcp` from `0.0.0.0/0`
  - allow `443/tcp` from `0.0.0.0/0`
  - allow `22/tcp` only from `10.8.0.0/24`
  - no public SSH rule

DNS:

- `petrotransit.fonefit.com` -> app EC2 Elastic IP
- `apipetrotransit.fonefit.com` -> app EC2 Elastic IP

## 4. OpenVPN Setup

Use the detailed runbook here:

- `deploy/aws/openvpn/README.md`

Your success criteria for the VPN box:

- the OpenVPN service is running
- your laptop can connect and receive a `10.8.0.x` address
- while connected, you can SSH to the app EC2 private IP
- while disconnected, public SSH to the app EC2 does not work

## 5. App EC2 Setup

After the VPN is working, SSH to the app EC2 over its private IP:

```bash
ssh -i <key.pem> ubuntu@<app-private-ip>
```

Install Docker and Git:

```bash
sudo apt update
sudo apt install -y docker.io docker-compose-v2 git
sudo usermod -aG docker $USER
```

Log out and back in so your user picks up the Docker group membership.

Clone the repo:

```bash
git clone <repo-url>
cd PetroTransit/deploy/aws/app
```

Create the deployment env file:

```bash
cp .env.example .env
```

## 6. App Environment Values

Fill `deploy/aws/app/.env` with the real values for:

```env
WEB_DOMAIN=petrotransit.fonefit.com
API_DOMAIN=apipetrotransit.fonefit.com
CADDY_EMAIL=ops@fonefit.com
VITE_API_BASE_URL=https://apipetrotransit.fonefit.com

POSTGRES_DB=petrotransit
POSTGRES_USER=petro
POSTGRES_PASSWORD=<strong-db-password>
ConnectionStrings__DefaultConnection=Host=db;Port=5432;Database=petrotransit;Username=petro;Password=<strong-db-password>
AppSettings__Token=<long-random-secret>
Frontend__BaseUrl=https://petrotransit.fonefit.com
Frontend__AllowedOrigins=https://petrotransit.fonefit.com
Seed__Email=<admin-email>
Seed__Username=<admin-username>
Seed__Password=<temporary-password>
Smtp__Host=<smtp-host>
Smtp__Port=587
Smtp__EnableSsl=true
Smtp__Username=<smtp-username>
Smtp__Password=<smtp-password>
Smtp__FromEmail=<sender@fonefit.com>
```

Notes:

- `VITE_API_BASE_URL` must stay on `https://apipetrotransit.fonefit.com`
- `Frontend__BaseUrl` and `Frontend__AllowedOrigins` should remain `https://petrotransit.fonefit.com`
- `ConnectionStrings__DefaultConnection` should point to the compose service name `db`
- do not commit `.env`

## 7. Migrating Data Off RDS

If the RDS database is disposable, skip this section. The API runs EF Core migrations automatically on startup and will create the schema in the containerized PostgreSQL instance.

If the RDS data must be preserved:

1. Start only the new PostgreSQL container:

```bash
cd <repo-root>/deploy/aws/app
docker compose up -d db
```

2. Install the PostgreSQL client on the app EC2 if it is not present:

```bash
sudo apt update
sudo apt install -y postgresql-client
```

3. Dump RDS to a plain SQL file:

```bash
PGPASSWORD='<rds-password>' pg_dump \
  -h <rds-endpoint> \
  -U <rds-user> \
  -d <rds-database> \
  --clean --if-exists --no-owner --no-privileges \
  > petrotransit-rds.sql
```

4. Restore the dump into the Docker PostgreSQL container:

```bash
docker compose exec -T db sh -lc 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB"' < petrotransit-rds.sql
```

5. Inspect the restore quickly:

```bash
docker compose exec db sh -lc 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -c "\\dt"'
```

6. Start the full app stack:

```bash
docker compose up -d --build
```

7. Validate the site before deleting RDS:
   - frontend loads
   - `/health` returns OK
   - existing users can log in
   - representative records are present

Take a final manual RDS snapshot only if your team wants rollback insurance. Otherwise, delete the RDS instance promptly to stop the monthly cost.

## 8. First Deployment

From `deploy/aws/app`:

```bash
docker compose up -d --build
```

Useful checks:

```bash
docker compose ps
docker compose logs -f caddy
docker compose logs -f api
docker compose logs -f web
docker compose logs -f db
```

Expected result:

- `db`, `caddy`, `api`, and `web` are running
- Caddy obtains certificates after DNS points to the app EC2 Elastic IP
- `https://petrotransit.fonefit.com` loads
- `https://apipetrotransit.fonefit.com/health` returns OK

## 9. DNS Cutover

In Route53, create or update:

- `petrotransit.fonefit.com`
- `apipetrotransit.fonefit.com`

Point both records to the app EC2 Elastic IP.

After DNS resolves:

- open the frontend URL in a browser
- open the API health URL directly
- confirm Caddy finished certificate issuance

## 10. Post-Deployment Validation

Validate these items in order:

1. Connect to OpenVPN from your laptop.
2. SSH to the app EC2 private IP over VPN.
3. Confirm the frontend loads over HTTPS.
4. Confirm `/health` responds on the API hostname.
5. Log in through the frontend.
6. Confirm authenticated API calls succeed.
7. Test password reset or another SMTP-backed flow.

## 11. Day-2 Operations

### Deploying a code update

On the app EC2:

```bash
cd <repo-root>
git pull
cd deploy/aws/app
docker compose up -d --build
```

Then verify:

```bash
docker compose ps
docker compose logs --tail=100 api
docker compose logs --tail=100 caddy
```

### Restarting services

Restart all containers:

```bash
cd <repo-root>/deploy/aws/app
docker compose restart
```

Restart one service:

```bash
docker compose restart api
docker compose restart web
docker compose restart caddy
```

### Viewing logs

```bash
docker compose logs --tail=200 api
docker compose logs --tail=200 web
docker compose logs --tail=200 caddy
```

Follow live logs:

```bash
docker compose logs -f api
```

### Checking container status

```bash
docker compose ps
docker ps
```

### Rebuilding from scratch on the app EC2

Use this only if a normal redeploy is not enough:

```bash
docker compose down
docker compose up -d --build
```

Do not delete named volumes unless you intend to remove:

- PostgreSQL application data
- ASP.NET data-protection keys
- Caddy certificate/state data

## 12. Backups And Data You Must Preserve

Important persisted data:

- Docker volume `pgdata`
- Docker volume `api_keys`
- Docker volume `caddy_data`
- Docker volume `caddy_config`
- OpenVPN CA and client/server certificate materials

What each one is for:

- `pgdata` stores application data
- `api_keys` keeps ASP.NET data-protection keys stable across container restarts
- `caddy_data` and `caddy_config` preserve issued TLS cert state
- OpenVPN PKI materials are required to issue or revoke admin access

At minimum:

- take regular PostgreSQL dumps from the Docker database
- securely back up the OpenVPN CA directory
- avoid deleting Docker named volumes casually

Database backup example:

```bash
cd <repo-root>/deploy/aws/app
docker compose exec -T db sh -lc 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB"' > petrotransit-backup.sql
```

## 13. Common Troubleshooting

### Frontend loads but API calls fail

Check:

- `VITE_API_BASE_URL` in `deploy/aws/app/.env`
- `Frontend__AllowedOrigins` in `deploy/aws/app/.env`
- `ConnectionStrings__DefaultConnection` points to `Host=db`
- `docker compose logs api`
- `https://apipetrotransit.fonefit.com/health`

### HTTPS does not come up

Check:

- Route53 records point to the app EC2 Elastic IP
- app EC2 security group allows `80/tcp` and `443/tcp`
- `docker compose logs caddy`

### SSH to app EC2 does not work

Check:

- you are connected to OpenVPN
- you are using the app EC2 private IP
- app EC2 security group allows `22/tcp` from `10.8.0.0/24`
- OpenVPN client actually received a `10.8.0.x` address

### OpenVPN works but app is unreachable

Check:

- app EC2 is running
- app EC2 security group allows `80/tcp` and `443/tcp`
- containers are healthy via `docker compose ps`
- Caddy logs do not show routing or cert issues

### API will not start because the database is unavailable

Check:

- `docker compose ps db`
- `docker compose logs --tail=200 db`
- `POSTGRES_DB`, `POSTGRES_USER`, and `POSTGRES_PASSWORD` in `.env`
- `ConnectionStrings__DefaultConnection` matches those values

### Password reset email does not send

Check:

- SMTP credentials in `.env`
- SES sender identity and region
- `docker compose logs api`

## 14. Safe Change Rules

- Keep OpenVPN on its own EC2.
- Keep SSH to the app EC2 restricted to the VPN subnet only.
- Keep the API on the separate public hostname.
- Do not replace Caddy with direct container port exposure.
- Do not publish the PostgreSQL container port publicly.
- Do not delete named Docker volumes unless you understand the recovery impact.

## 15. Quick Command Reference

App deploy:

```bash
cd <repo-root>/deploy/aws/app
docker compose up -d --build
```

App status:

```bash
docker compose ps
```

App logs:

```bash
docker compose logs --tail=200 api
docker compose logs --tail=200 caddy
docker compose logs --tail=200 web
docker compose logs --tail=200 db
```

VPN service status:

```bash
sudo systemctl status openvpn-server@server
```

VPN logs:

```bash
sudo journalctl -u openvpn-server@server -n 100 --no-pager
```
