# PetroTransit AWS Deployment

For the complete build, deployment, and day-2 operations guide, start with:

- `deploy/aws/OPERATIONS.md`

This repo is now prepared for the final two-EC2 production layout:

- `EC2 #1`: OpenVPN server, installed directly on the host
- `EC2 #2`: app server running Docker for `postgres + caddy + web + api`

Public production URLs remain:

- `https://petrotransit.fonefit.com`
- `https://apipetrotransit.fonefit.com`

Caddy on the app EC2 handles:

- reverse proxy
- automatic TLS
- host-based routing for frontend vs API

SSH for the app EC2 is not exposed publicly. Administrative SSH access should be allowed only from the VPN client subnet, such as `10.8.0.0/24`.

## Recommended AWS shape

Use:

- 1 public OpenVPN EC2 with Elastic IP
- 1 public app EC2 with Elastic IP
- 2 Route53 `A` records pointing to the app EC2 Elastic IP
- SES SMTP credentials for outbound email

Recommended instance roles:

- OpenVPN EC2:
  - Ubuntu 24.04
  - OpenVPN installed directly on the host
  - public `1194/udp`
- App EC2:
  - Ubuntu 24.04
  - Docker Engine and Docker Compose plugin
  - `postgres` container on the internal Docker network
  - `caddy` container on `80/443`
  - `web` container on internal port `80`
  - `api` container on internal port `8080`

There is no ALB in this design. Caddy terminates TLS directly on the app EC2.

## Deployment files

Primary app-host deployment files:

- `deploy/aws/app/docker-compose.yml`
- `deploy/aws/app/.env.example`
- `deploy/aws/app/Caddyfile`

OpenVPN setup guidance:

- `deploy/aws/openvpn/README.md`

Only supported app deployment layout:

- `deploy/aws/app/`

## DNS

Create these Route53 records and point both at the app EC2 Elastic IP:

- `petrotransit.fonefit.com`
- `apipetrotransit.fonefit.com`

Caddy will request and renew the TLS certificates automatically after DNS resolves correctly.

## Security groups

OpenVPN EC2 security group:

- allow `1194/udp` from `0.0.0.0/0`
- allow `22/tcp` only from your office IP or your preferred management source

App EC2 security group:

- allow `80/tcp` from `0.0.0.0/0`
- allow `443/tcp` from `0.0.0.0/0`
- do **not** allow `22/tcp` from `0.0.0.0/0`
- allow `22/tcp` only from the VPN client CIDR, such as `10.8.0.0/24`

Do not expose PostgreSQL publicly. The `db` container stays on the internal Docker network only.

## SMTP

The API already supports runtime-injected SMTP settings. No code change is required for AWS.

Set these in the app deployment `.env`:

- `Smtp__Host`
- `Smtp__Port`
- `Smtp__EnableSsl`
- `Smtp__Username`
- `Smtp__Password`
- `Smtp__FromEmail`

If these values are missing, the API falls back to fake dev logging and does not send real email.

## Frontend build-time note

`VITE_API_BASE_URL` is a frontend build-time setting, not a normal runtime API env var.

For production builds, keep it pointed at the public API hostname:

```env
VITE_API_BASE_URL=https://apipetrotransit.fonefit.com
```

## App runtime env vars

Set these in `deploy/aws/app/.env`:

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
Seed__Password=<temporary-strong-password>
Smtp__Host=<smtp-host>
Smtp__Port=587
Smtp__EnableSsl=true
Smtp__Username=<smtp-username>
Smtp__Password=<smtp-password>
Smtp__FromEmail=<sender@fonefit.com>
```

Do not commit real values to the repo.

The `ConnectionStrings__DefaultConnection` values should match `POSTGRES_DB`, `POSTGRES_USER`, and `POSTGRES_PASSWORD`.

## Migrating Off RDS

If the current RDS instance has no data worth keeping, you can skip the dump and let the API create the schema on first boot by running EF Core migrations automatically.

If you need to keep the current data:

1. On the app EC2, copy `deploy/aws/app/.env.example` to `.env` and fill in the final container-based database values.
2. Start only PostgreSQL first:

```bash
cd deploy/aws/app
docker compose up -d db
```

3. Install the PostgreSQL client on the app EC2 if needed:

```bash
sudo apt update
sudo apt install -y postgresql-client
```

4. Dump the RDS database:

```bash
PGPASSWORD='<rds-password>' pg_dump \
  -h <rds-endpoint> \
  -U <rds-user> \
  -d <rds-database> \
  --clean --if-exists --no-owner --no-privileges \
  > petrotransit-rds.sql
```

5. Restore into the Docker PostgreSQL container:

```bash
docker compose exec -T db sh -lc 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB"' < petrotransit-rds.sql
```

6. Start the full stack:

```bash
docker compose up -d --build
```

7. Verify the application works before deleting RDS:

- `https://petrotransit.fonefit.com`
- `https://apipetrotransit.fonefit.com/health`
- login and a few representative reads/writes

After verification, delete the RDS instance and remove its security group/rules. If your team wants a rollback point, take a final manual snapshot first.

## Deployment workflow

1. Launch the OpenVPN EC2 in the target VPC and attach an Elastic IP.
2. Install and configure OpenVPN on the host.
3. Launch the app EC2 in the same VPC and attach an Elastic IP.
4. Apply the app EC2 security group so SSH is allowed only from the VPN client CIDR.
5. Install Docker Engine and Docker Compose on the app EC2.
6. Clone this repo onto the app EC2.
7. Copy `deploy/aws/app/.env.example` to `deploy/aws/app/.env`.
8. Fill in the real domain, database, token, seed, and SMTP values.
9. If needed, migrate data from RDS into the Docker PostgreSQL container.
10. From `deploy/aws/app`, run:

```bash
docker compose up -d --build
```

11. Create Route53 records for both hostnames pointing at the app EC2 Elastic IP.
12. Verify:
   - connect to OpenVPN successfully
   - SSH to the app EC2 over private IP only after VPN connection
   - `https://petrotransit.fonefit.com`
   - `https://apipetrotransit.fonefit.com/health`
   - login flow
   - password reset email

## Notes for Arm instances

If you use a Graviton instance such as `t4g`, the current Docker base images for .NET 9, Node 20, nginx, and Caddy support ARM64, so building directly on the app EC2 is a reasonable path.

## Next Steps

1. Provision the OpenVPN EC2 and validate client connectivity.
2. Provision the app EC2 with locked-down SSH.
3. Fill in `deploy/aws/app/.env` with the Docker PostgreSQL values.
4. Migrate RDS data if it must be preserved.
5. Deploy the Docker stack on the app EC2.
6. Point Route53 DNS at the app EC2 Elastic IP.
7. Validate frontend, API, auth, and email behavior.
8. Delete the RDS instance after verification.
