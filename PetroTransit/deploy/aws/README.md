# PetroTransit AWS Deployment

This repo is now prepared for the two-public-subdomain layout:

- `https://petrotransit.fonefit.com` -> frontend EC2 target group
- `https://apipetrotransit.fonefit.com` -> API EC2 target group

This follows the supervisor's ALB + EC2 + Docker approach.

## Recommended AWS shape

Use:

- 1 Application Load Balancer
- 2 target groups
- 2 Linux EC2 instances or 2 small Auto Scaling groups
- 1 RDS PostgreSQL database
- 1 ACM certificate covering both subdomains
- Route53 DNS records for both subdomains
- SES SMTP credentials for outbound email

The clean split is:

- Web EC2 runs the frontend nginx container on port `80`
- API EC2 runs an nginx container on port `80` that proxies to the .NET API container on port `8080`

This keeps the ALB config simple and matches the requested host-based routing model.

## ALB routing

Create these HTTPS listener rules on the ALB:

- Host `petrotransit.fonefit.com` -> web target group
- Host `apipetrotransit.fonefit.com` -> api target group

Use ACM for the TLS certificate on the ALB listener.

Point health checks to:

- Web target group: `/`
- API target group: `/health`

## DNS

Create these records in Route53:

- `petrotransit.fonefit.com` -> alias to ALB
- `apipetrotransit.fonefit.com` -> alias to ALB

## RDS

Provision a PostgreSQL database and set the API connection string to point to it.

Do not deploy the local Docker `db` service to AWS.

## SMTP

The API already supports runtime-injected SMTP settings. No code change is required for AWS.

Set these in the API environment:

- `Smtp__Host`
- `Smtp__Port`
- `Smtp__EnableSsl`
- `Smtp__Username`
- `Smtp__Password`
- `Smtp__FromEmail`

If these values are missing, the API falls back to fake dev logging and does not send real email.

## Frontend build-time note

`VITE_API_BASE_URL` is a frontend build-time setting, not a normal runtime API env var.

For production builds, it must be:

```env
VITE_API_BASE_URL=https://apipetrotransit.fonefit.com
```

## API runtime env vars

Set these in the API environment:

```env
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__DefaultConnection=Host=...;Port=5432;Database=...;Username=...;Password=...
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

## Deployment files in this folder

- `deploy/aws/web/docker-compose.yml`
- `deploy/aws/web/.env.example`
- `deploy/aws/api/docker-compose.yml`
- `deploy/aws/api/.env.example`
- `deploy/aws/api/nginx.conf`

These are deployment templates for EC2-hosted Docker, not local development files.

## Suggested EC2 workflow

1. Launch the web EC2 and API EC2 in the same VPC as the ALB and RDS.
2. Install Docker and Docker Compose plugin on both instances.
3. Clone this repo onto both instances.
4. On the web EC2:
   - copy `deploy/aws/web/.env.example` to an untracked `.env`
   - fill in `VITE_API_BASE_URL`
   - run `docker compose up -d --build` from `deploy/aws/web`
5. On the API EC2:
   - copy `deploy/aws/api/.env.example` to an untracked `.env`
   - fill in the real runtime values
   - run `docker compose up -d --build` from `deploy/aws/api`
6. Register the EC2 instances in the ALB target groups.
7. Configure Route53 aliases to the ALB.
8. Verify:
   - `https://petrotransit.fonefit.com`
   - `https://apipetrotransit.fonefit.com/health`
   - login
   - password reset email

## Remaining manual AWS work

- create security groups
- create the ALB
- create Route53 records
- create ACM certificate
- create RDS PostgreSQL
- create SES SMTP credentials
- inject the real environment values

