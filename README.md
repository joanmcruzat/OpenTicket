# OpenTicket

A lightweight, free, self-hostable issue tracker for software teams, with a Jira-style board and workflow. No subscription fees and no paid services.

**Stack:** ASP.NET Core 10 (minimal APIs) · EF Core 10 + PostgreSQL 17 · ASP.NET Core Identity + JWT · Angular 22 + Angular Material · Docker · GitHub Actions.

User guide: [docs/user-guide.md](docs/user-guide.md) · Design: [docs/architecture.md](docs/architecture.md) · Deploy for free: [docs/deployment.md](docs/deployment.md) · Progress: [docs/development-checklist.md](docs/development-checklist.md)

## Features

- **Three roles.**
  - **Reporters** file bugs and requests, follow progress, comment, and can drop or reopen their own tickets.
  - **Developers** see every ticket, pick up unassigned work with "Assign to me", and move their tickets across the board.
  - **Admins** move any ticket, assign anyone, and manage users and categories.
- **Workflow.** Backlog → To Do → In Progress → In Review → Done, plus Won't Do (with a reason). Open tickets move freely; closed ones reopen to To Do.
- **Sprints.** Plan on the Backlog page, start a sprint to put it on the board, and complete it to move unfinished work on.
- **Kanban board.** Drag cards between columns. Only valid moves are offered. Filter by type, category, assignee or text.
- **Issue types.** Bug, Task, Story and Improvement, with priorities Low, Medium, High and Urgent.
- **History.** Comments are immutable, and every change to a ticket is recorded in its history.
- **Dashboard.** Counts per status, plus "assigned to me" and "unassigned".
- **Demo mode and in-app guide.** A public demo with sample data and one-click sign-in for each role. Every user gets a Guide page and "Try this" tips.
- **Sign-in options.** Email and password with a strength meter, "Forgot password?" by email, and optional "Continue with GitHub / Google".
- **Security.**
  - Short-lived access tokens kept in memory, plus rotating refresh tokens in an HttpOnly cookie with reuse detection.
  - Account lockout and rate-limited sign-in.
  - Deactivation and role changes take effect immediately.
  - Users only ever see tickets in their own scope; others return 404.

## Run it locally (development)

Prerequisites: .NET SDK 10.0.402+ (pinned in `global.json`), Node 24+, Docker.

```bash
cp .env.example .env                     # set a strong POSTGRES_PASSWORD
docker compose up -d --wait db           # PostgreSQL on 127.0.0.1:55432
dotnet tool restore                      # dotnet-ef
```

Configure the API's local secrets once. They are stored outside the repository by `dotnet user-secrets`:

```bash
cd src/OpenTicket.API
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=55432;Database=openticket;Username=openticket;Password=<POSTGRES_PASSWORD>"
dotnet user-secrets set "Jwt:SigningKey" "$(openssl rand -base64 48)"
dotnet user-secrets set "Seed:AdminEmail" "admin@openticket.local"
dotnet user-secrets set "Seed:AdminPassword" "<a password of 10+ characters>"
cd ../..
```

Start both halves:

```bash
dotnet run --project src/OpenTicket.API --launch-profile http   # http://localhost:5080 (applies migrations in Development)
npm --prefix web install && npm --prefix web start              # http://localhost:4200, proxies /api to :5080
```

To try the demo locally, run `dotnet user-secrets set "Demo:Enabled" "true"` in `src/OpenTicket.API` and restart the API.

In development, password-reset emails are written to the API log instead of being sent (`Email:LogOnly` in `appsettings.Development.json`).

Sign in with the seed admin. Create a category or two (e.g. Frontend, Backend), and add developers under **Users**. Reporters sign up themselves at `/register`.

API docs (Development only): <http://localhost:5080/swagger>.

## Run the whole stack in Docker (self-hosted)

Set `JWT_SIGNING_KEY`, `SEED_ADMIN_EMAIL` and `SEED_ADMIN_PASSWORD` in `.env`, then run:

```bash
docker compose --profile app up -d --build   # db → one-shot migrate → app on http://127.0.0.1:8080
```

Put a TLS reverse proxy (Caddy, or a Cloudflare Tunnel) in front of it. See [docs/deployment.md](docs/deployment.md#option-b-self-hosted-with-docker).

## Tests

```bash
dotnet test                               # 201 tests: domain unit tests + API integration tests (Testcontainers PostgreSQL; needs Docker)
npm --prefix web test -- --watch=false    # Angular unit tests (Vitest)
```

## Project layout

```
src/OpenTicket.Domain           Entities and the ticket workflow/permission rules (no dependencies)
src/OpenTicket.Application      Use-case services, DTOs, interfaces
src/OpenTicket.Infrastructure   EF Core + PostgreSQL, Identity, JWT, migrations
src/OpenTicket.API              Minimal-API endpoints, auth, ProblemDetails, serves the Angular build
web/                            Angular 22 single-page app
tests/                          xUnit v3 unit and integration tests
```

## Configuration

| Setting (environment variable) | Purpose |
|---|---|
| `ConnectionStrings__Default` | PostgreSQL connection string (Npgsql format) |
| `Jwt__SigningKey` | Base64 key, ≥ 32 bytes. **Required.** |
| `Jwt__AccessTokenMinutes` / `Jwt__RefreshTokenDays` | Defaults 15 / 7 |
| `Seed__AdminEmail` / `Seed__AdminPassword` | Creates the first admin on startup, only if none exists |
| `RateLimiting__AuthPermitLimit` | Sign-in/up/refresh requests per IP per minute (default 10) |
| `ForwardedHeaders__Enabled` | `true` behind a reverse proxy (set in the Docker image) |
| `App__PublicUrl` | The address users open, e.g. `https://tickets.example.com`. Used in email links. |
| `Email__SmtpHost` / `Email__SmtpPort` / `Email__SmtpUsername` / `Email__SmtpPassword` / `Email__From` | SMTP for password-reset emails. Empty host = reset by email is off. `Email__LogOnly=true` logs emails instead (development only). |
| `Authentication__GitHub__ClientId` / `__ClientSecret` | Shows "Continue with GitHub" when both are set |
| `Authentication__Google__ClientId` / `__ClientSecret` | Shows "Continue with Google" when both are set |
| `Demo__Enabled` / `Demo__ResetIntervalHours` | Public demo mode (default off; reset every 6 h). Dedicated demo instances only. |

Never commit secrets. Locally, use `dotnet user-secrets` and the git-ignored `.env`. In production, use your host's environment settings and GitHub Actions secrets.

## Database migrations

```bash
dotnet ef migrations add <Name> -p src/OpenTicket.Infrastructure -s src/OpenTicket.API -o Persistence/Migrations
dotnet ef migrations script --idempotent -p src/OpenTicket.Infrastructure -s src/OpenTicket.API   # review the SQL
```

Production migrations run from CI (an EF bundle) or via `docker compose --profile app up` (the `migrate` service). They never run on app startup.

## License

[MIT](LICENSE)
