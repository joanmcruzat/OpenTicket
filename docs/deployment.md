# Deploying OpenTicket for free

Two supported paths:

- **Option A: free hosted.** Render (app) + Neon (PostgreSQL) + GitHub Actions. $0/month and no server to look after. The app sleeps when idle.
- **Option B: self-hosted with Docker.** Any Linux machine or free VM. No cold starts. You run the box.

> Free-tier limits change. The figures below are what the plan was built on (November 2026). Check each provider's current pricing page before you rely on them.

---

## Option A: Render + Neon + GitHub Actions

```
GitHub push to main ──► CI: build · test · image ──► migrate Neon (EF bundle) ──► Render deploy hook
Browser ──HTTPS──► Render free web service (one container: API + Angular) ──SSL──► Neon PostgreSQL
```

What to expect on free tiers:
- **Render** sleeps the service after about 15 minutes without traffic. The next visitor waits about 30–60 seconds while it starts.
- **Neon** suspends the database when idle and resumes in under a second.

Fine for a small team. If the cold start bothers you, use Option B.

### 1. Database: Neon

1. Create a free account at neon.tech, then a project (PostgreSQL 17). Pick the region closest to where you'll run Render.
2. Open **Connect** and copy the connection details: host, database, user and password.
3. Write them as an **Npgsql** connection string. Npgsql doesn't accept the `postgresql://` URL form:

   ```
   Host=<endpoint>.neon.tech;Database=<db>;Username=<user>;Password=<password>;SSL Mode=Require
   ```

Keep this string secret. Below it's called `DATABASE_URL`.

### 2. Repository and CI secrets: GitHub

1. Push this repository to GitHub. Actions minutes are free for public repositories, and private ones get a monthly allowance.
2. Under **Settings → Secrets and variables → Actions**:
   - Secret `DATABASE_URL`: the Npgsql string from step 1.
   - Variable `DEPLOY_ENABLED` = `true`.
3. Under **Settings → Environments**, create `production`. Optionally add yourself as a required reviewer, so every deploy waits for your approval.
4. Run the workflow once by hand: **Actions → CI → Run workflow** on `main`. The `deploy` job builds an EF Core migration bundle and creates the schema in Neon. It warns that no Render hook is set yet, which is expected.

### 3. App: Render

1. Create a free account at render.com. Choose **New → Web Service → Build and deploy from a Git repository** and pick this repository.
2. Settings:
   - **Runtime:** Docker (Render builds the repository's `Dockerfile`)
   - **Instance type:** Free
   - **Health check path:** `/health/live`
   - **Auto-Deploy:** **Off**. CI triggers deploys only after migrations succeed.
3. Environment variables:

   | Key | Value |
   |---|---|
   | `PORT` | `8080` (the port the container listens on) |
   | `ConnectionStrings__Default` | the Neon Npgsql string |
   | `Jwt__SigningKey` | output of `openssl rand -base64 48` |
   | `Seed__AdminEmail` | your admin email |
   | `Seed__AdminPassword` | a strong password (10+ characters). Change it after first sign-in. |

4. Deploy. When the service is live, open `https://<service>.onrender.com`, sign in as the admin, and use **Change password** in the account menu.
5. Copy **Settings → Deploy Hook** into a GitHub secret named `RENDER_DEPLOY_HOOK_URL`.

From now on, every push to `main` runs the tests, applies migrations to Neon, then deploys to Render.

**Custom domain (optional):** add it under the Render service's **Settings → Custom Domains** and create the DNS record Render shows. Render provides the HTTPS certificate.

### 4. Optional: password reset by email and social sign-in

Both are off until configured, and the sign-in page only shows what is set up. Add the variables in Render under **Environment**; for Docker, put them in `.env` (see `.env.example`).

**Password reset by email** needs a public URL and any SMTP provider. Brevo and Resend both have free tiers; check their current limits.

| Render variable | Value |
|---|---|
| `App__PublicUrl` | `https://<service>.onrender.com` (or your custom domain) |
| `Email__SmtpHost` / `Email__SmtpPort` | From your provider, usually port 587 |
| `Email__SmtpUsername` / `Email__SmtpPassword` | SMTP credentials or API key |
| `Email__From` | `OpenTicket <no-reply@your-domain>`; the domain must be verified with the provider |

**GitHub sign-in:**

1. Open **GitHub → Settings → Developer settings → OAuth Apps → New OAuth App**.
2. Set the homepage to your public URL.
3. Set the callback URL to `https://<your-host>/api/v1/auth/external/github/callback`.
4. Copy the client ID and a new client secret into `Authentication__GitHub__ClientId` and `Authentication__GitHub__ClientSecret`.

**Google sign-in:**

1. Open **Google Cloud Console → APIs & Services**.
2. Configure the OAuth consent screen. The scopes needed are email and profile.
3. Go to **Credentials → Create OAuth client ID** and choose "Web application".
4. Add the authorised redirect URI `https://<your-host>/api/v1/auth/external/google/callback`.
5. Copy the client ID and secret into `Authentication__Google__ClientId` and `Authentication__Google__ClientSecret`.

How accounts are matched:

- **First sign-in.** A person signing in with a provider for the first time is linked to the existing account with the same verified email.
- **No matching account.** A new account is created as a Reporter.
- **Unverified email.** Provider accounts without a verified email are refused.

### 5. Optional: run it as a public demo

A demo server lets anyone try OpenTicket without signing up.

- **Separate instance.** Use its own Render service and its own Neon database, not your real one.
- **Turn it on.** Add `Demo__Enabled=true` and, optionally, `Demo__ResetIntervalHours`. The default reset interval is 6 hours; `0` turns resets off.

What demo mode does:

- **Sample data.** On startup it seeds a small team (an admin, three developers, two reporters), four categories and 17 tickets covering every status and type, with comments and history.
- **One-click sign-in.** The sign-in page offers **Explore the live demo** buttons for Developer, Reporter and Admin. No passwords are ever published: demo accounts have random passwords, and visitors get in through `POST /api/v1/auth/demo/{role}`.
- **Locked down.**
  - Sign-up is turned off.
  - Demo accounts can't be deactivated, re-roled or have their password reset.
  - Password-reset emails are never sent to demo addresses (`*.invalid`).
- **Regular resets.**
  - Tickets reported by demo accounts are deleted and seeded again.
  - Data created by other users is never touched.
  - Users a visitor adds as Admin are kept. The reset doesn't remove them, so re-create the demo database now and then if that matters to you.
- **Banner.** A yellow banner tells visitors they are on a demo and lets them switch role.

> Never enable demo mode on a real instance: anyone could sign in as an admin.

### 6. First-time setup in the app

1. **Categories:** create a few (e.g. Frontend, Backend, Infrastructure). Nobody can create tickets until at least one exists.
2. **Users → Add user:** create your developers with temporary passwords. They change them via **Change password**.
3. Share the site URL. Reporters sign up themselves.

---

## Option B: self-hosted with Docker

Works on any Linux machine with Docker: a home server, an old PC, a Raspberry Pi 5, or a free cloud VM such as Oracle Cloud's Always Free tier.

```bash
git clone <your repo> openticket && cd openticket
cp .env.example .env
# Edit .env: POSTGRES_PASSWORD, JWT_SIGNING_KEY (openssl rand -base64 48), SEED_ADMIN_EMAIL, SEED_ADMIN_PASSWORD
docker compose --profile app up -d --build
```

This starts PostgreSQL, runs the one-shot `migrate` service, then starts the app on `127.0.0.1:8080`. It listens on localhost only on purpose: put HTTPS in front of it.

- **Caddy** (automatic Let's Encrypt): a two-line `Caddyfile`, `tickets.example.com { reverse_proxy 127.0.0.1:8080 }`.
- **Cloudflare Tunnel** (free; no open ports, works behind home routers): point the tunnel at `http://127.0.0.1:8080`.

**Upgrading:** `git pull && docker compose --profile app up -d --build`. The `migrate` service applies new migrations before the app restarts.

---

## Backups and restore

Neon keeps a short point-in-time restore window on the free plan. Still, take your own backups: it's your data.

```bash
# Backup (Neon: use the host/user/db from its dashboard; self-hosted: docker compose exec db ...)
pg_dump "host=<host> dbname=<db> user=<user> sslmode=require" --format=custom --file=openticket-$(date +%F).dump

# Restore into an empty database
pg_restore --no-owner --dbname="host=<host> dbname=<db> user=<user> sslmode=require" openticket-2026-11-07.dump
```

Self-hosted shortcut:

```bash
docker compose exec -T db pg_dump -U openticket -Fc openticket > openticket-$(date +%F).dump
```

Schedule it with cron, and keep copies somewhere other than the server itself.

---

## Troubleshooting

| Symptom | Cause / fix |
|---|---|
| First page load takes up to a minute | Render free instance waking up. That's expected on Option A. |
| App fails at start with `Jwt:SigningKey must be a base64 string of at least 32 bytes` | Set `Jwt__SigningKey` (Render) or `JWT_SIGNING_KEY` (`.env`). |
| App fails at start with `relation "role" does not exist` | Migrations haven't run. Run the GitHub workflow (Option A) or `docker compose --profile app up migrate` (Option B). |
| No "Forgot password?" link | Set both `App__PublicUrl` and `Email__SmtpHost` (+ `Email__From`). |
| GitHub/Google says "redirect_uri mismatch" | The callback URL registered at the provider must match exactly, including `https` and the host. Behind a proxy keep `ForwardedHeaders__Enabled=true`. |
| `No admin exists…` warning in logs | `Seed__AdminEmail`/`Seed__AdminPassword` weren't set on the first start. Set them and restart. |
| Everyone gets 429 on sign-in | More than 10 sign-in attempts per minute from one IP. Raise `RateLimiting__AuthPermitLimit` if many users share one office IP. |
| Log line `Cannot load library libgssapi_krb5.so.2` | Harmless. The minimal runtime image doesn't include Kerberos, and OpenTicket doesn't use it. |
