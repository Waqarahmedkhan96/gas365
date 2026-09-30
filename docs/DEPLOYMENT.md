# Deployment runbook

Gas365 runs as one Docker Compose stack on a Linux server: Caddy (HTTPS) → web (nginx and the React app) → API → PostgreSQL. The same files serve staging (an EU server, for testing from Denmark) and production (a Pakistani server). Moving between servers means installing Docker, copying the configuration and restoring a backup. No production deployment happens without the owner's explicit approval.

## 1. Server preparation (once per server)

1. **Server:** Ubuntu Server 24.04 LTS, at least 2 vCPU, 4 GB RAM and 40 GB SSD. For EU staging, a Hetzner Cloud CX22/CX32 or similar; for production, a comparable Pakistani VPS with daily provider snapshots.
2. **User and firewall:** create a non-root sudo user with SSH key login only, and disable password and root SSH login. Allow only ports 22, 80 and 443 (`ufw allow OpenSSH; ufw allow 80; ufw allow 443; ufw enable`). Enable automatic security updates (`unattended-upgrades`).
3. **Docker:** install Docker Engine and the Compose plugin from Docker's official repository. Add the deploy user to the `docker` group.
4. **Deployment folder:** create `/opt/gas365` owned by the deploy user. Copy `deploy/.env.example` to `/opt/gas365/.env`, fill in every value, and `chmod 600 .env`. Generate the database password with `openssl rand -hex 32`.
5. **DNS:** point the domain's A record (for example `staging.gas365.pk`, later `gas365.pk`) at the server. Caddy obtains and renews HTTPS certificates automatically once DNS resolves.
6. **Email:** configure SMTP from a transactional email provider. Publish SPF, DKIM and DMARC records for the sending domain so verification and password-reset emails are delivered.
7. **Off-site backups:** install `rclone`, configure a remote (for example S3-compatible object storage or a Hetzner Storage Box) and set `BACKUP_REMOTE`. Optionally set `BACKUP_HEARTBEAT_URL` to a healthchecks.io check that alerts when backups stop.
8. **Backup schedule:** add a cron entry for nightly backups: `30 2 * * * /opt/gas365/backup.sh nightly >> /var/log/gas365-backup.log 2>&1`.

## 2. GitHub configuration (once)

1. Create two environments: **staging** and **production**. On **production**, add yourself as a required reviewer, so no production release starts without approval.
2. Add these secrets to each environment:
   - `DEPLOY_HOST`
   - `DEPLOY_USER`
   - `DEPLOY_PATH` (`/opt/gas365`)
   - `DEPLOY_SSH_KEY` (a dedicated deploy key)
   - `DEPLOY_KNOWN_HOSTS` (the output of `ssh-keyscan <host>`)
3. Images are pushed to `ghcr.io/waqarahmedkhan96`. Make the packages private and allow the server to pull them (`docker login ghcr.io` with a read-only token), or make them public.

## 3. Releasing

- **Staging:** every merge to `main` that passes "Platform checks" builds images tagged with the commit SHA, scans them (no HIGH or CRITICAL findings allowed), pushes them and deploys to staging automatically.
- **Production:** run the **Release** workflow manually with `environment = production` and the tested staging tag. GitHub waits for the required reviewer's approval before the job starts.
- **What `deploy.sh` does:**
  1. takes a pre-deploy backup;
  2. pulls the images;
  3. runs migrations once in the `migrate` job;
  4. starts the new API and web;
  5. checks `https://<domain>/health/ready`.

  If anything fails, it redeploys the previous tag automatically. Migrations are additive and backward compatible, so the previous version works against the newer schema.

## 4. Rollback

Run the **Release** workflow manually with the previous tag, or on the server run `./deploy.sh <previous-tag>`. The deployed tag is recorded in `.deployed-tag`.

If a data problem needs restoring rather than a code rollback, see section 5. Never restore over the live database.

## 5. Restore

1. `./restore.sh backups/<file>.dump gas365_restore_check` restores into a **new** database and prints the migration count and key row counts.
2. Check the restored data. Then stop `api` and `web`, set `POSTGRES_DB=gas365_restore_check` in `.env` and run `./deploy.sh <current-tag>`. After confirming the restore, rename the databases during a maintenance window.
3. The restore procedure is exercised locally with `node scripts/verify-restore.mjs`. It dumps, restores into an isolated database, compares row counts, applies this release's migrations and drops the copy. Run it before each release and quarterly on staging with a production backup.

## 6. Optional Google sign-in

Google sign-in is off until both keys are set (owner decision, 2026-09-30).

1. Open [Google Cloud Console](https://console.cloud.google.com/) and create a project named Gas365.
2. Under **APIs & Services → OAuth consent screen**, choose **External**, enter the app name Gas365, your support email and the domain, and add the scopes `openid`, `email` and `profile`. Publish the app.
3. Under **Credentials → Create credentials → OAuth client ID**, choose **Web application**.
4. Add the authorised redirect URI `https://<your domain>/api/auth/google/callback` (for example `https://gas365.pk/api/auth/google/callback`). Add the staging domain too if you test there.
5. Copy the client ID and secret into `GOOGLE_CLIENT_ID` and `GOOGLE_CLIENT_SECRET` in the server's `.env` (never in Git), then redeploy.

Behaviour: a Google-verified email that matches an existing account is linked to it; a new Google user finishes sign-up with a name and business name and gets the normal 14-day trial. Unverified Google emails are refused. Google accounts have no password until the owner uses "Forgot password".

## 7. Monitoring and alerting

| Signal          | How                                                                                                                                                   | Alert                                                    |
| --------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------- |
| Availability    | An external uptime monitor (for example UptimeRobot) on `https://<domain>/health/ready` every minute                                                  | Not 200, or body not `Healthy`/`Degraded`, for 2 minutes |
| Background jobs | `/health/ready` reports `Degraded` when a job failed 3 times in a row or the notification sweep stalled for 15 minutes; `JobStates` holds the details | Degraded for 15 minutes                                  |
| Backups         | `BACKUP_HEARTBEAT_URL` pinged after each successful, verified backup                                                                                  | No ping for 26 hours                                     |
| Disk            | Provider disk alerts or a cron `df` check                                                                                                             | Above 80% used                                           |
| Certificates    | Caddy renews automatically; the uptime monitor catches failures                                                                                       | HTTPS error                                              |
| Logs            | Structured JSON from the API (`docker compose logs api`), with no passwords, tokens or record contents                                                | On investigation                                         |

## 8. Security checklist per environment

- Secrets exist only in the server `.env` (mode 600) and GitHub environment secrets, never in the repository. CI runs gitleaks on the full history.
- `ASPNETCORE_ENVIRONMENT=Production` makes the API refuse to start with missing or unsafe settings, including an HTTP public origin or a default password.
- PostgreSQL has no published port. Only Caddy (80/443) is reachable from the internet.
- The API trusts forwarded headers only from private proxy networks, so rate limits and audit use the real client address.
- `PLATFORM_ADMIN_EMAILS` lists only the platform owner's addresses.
