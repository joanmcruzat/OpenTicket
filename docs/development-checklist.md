# OpenTicket — Development Checklist

Mark an item done only after it has been built and tested. Note how each item was verified.

## Phase 1 — Requirements, architecture, schema, ERD
- [x] System architecture ([architecture.md §1](architecture.md))
- [x] ERD (Mermaid) and schema (§2–3)
- [x] Workflow state machine (§4)
- [x] API specification (§5)
- [x] Role/permission matrix (§6)
- [x] Angular page structure (§7)
- [x] Milestones (§8)
- [x] **Architecture reviewed and approved by owner** (2026-11-07)
- [x] All review questions answered and applied (§9)

## Phase 2 — Solution & PostgreSQL
- [x] git init, `.gitignore`, `.editorconfig`, `global.json`, `Directory.Build.props` (nullable, warnings as errors, analyzers), central package versions, lock files
- [x] Solution with 4 src + 2 test projects, references per Clean Architecture
- [x] docker-compose with PostgreSQL 17 for local dev (port 55432)
- [x] Domain entities, DbContext, entity configurations, snake_case naming
- [x] Initial migration created and SQL reviewed (fixed a NULL-passing check constraint and Identity index names)
- [x] `/health/live` + `/health/ready`; API started against compose DB, migration applied, both endpoints 200 *(verified 2026-11-07)*
- [x] Tests: 10 unit (domain) + 6 integration (Testcontainers PostgreSQL: health, ticket numbering, check constraints, case-insensitive category uniqueness), 8/8 consecutive green runs
- [ ] Phase 2 committed

## Phase 3 — Authentication & authorization *(verified 2026-11-07)*
- [x] Identity with Guid keys, roles seeded, first-admin seed from config
- [x] Register / login / refresh / logout / me (+ change-password)
- [x] Refresh-token rotation, hashed storage, reuse detection, HttpOnly SameSite=Strict cookie
- [x] Global exception handler + ProblemDetails (traceId, stable `code`)
- [x] Rate limiting on auth endpoints; lockout after 5 failures
- [x] Per-request active/role check (deactivation is immediate)
- [x] Integration tests green

## Phase 4 — Tickets & workflow *(verified)*
- [x] Domain workflow with unit tests for every transition and permission
- [x] CRUD endpoints, scoping (404 outside scope), pagination/filter/sort/search
- [x] Optimistic concurrency (xmin → 409)
- [x] Cancel with required reason → Closed (Cancelled)

## Phase 5 — Comments, assignment, history *(verified)*
- [x] Comments endpoints (immutable, read-only once closed)
- [x] Assign / unassign (admin only, active agents only)
- [x] History recorded for every change, names resolved for display

## Phase 6 — Dashboard, categories, users *(verified)*
- [x] Dashboard summary (role-scoped, one grouped query)
- [x] Category management (case-insensitive unique names, activate/deactivate)
- [x] User management (create, role change, activate/deactivate, reset password, self-protection)

## Phase 7 — Angular frontend *(verified in browser)*
- [x] Angular 22, zoneless, standalone, OnPush, signals, Material 3, strict templates
- [x] Auth pages, guards, interceptors (refresh once + retry), session restore on reload
- [x] Dashboard, ticket list, create, detail (confirmation banner, comments, history, assignment)
- [x] Admin: users, categories
- [x] Build within budget + 16 Vitest tests; full customer → admin → agent → customer workflow clicked through; 375 px layout checked

## Phase 8 — Tests *(verified)*
- [x] Authorization matrix: every non-public route → 401 anonymously (auto-discovered); admin-only routes → 403 for agents/customers
- [x] 133 .NET tests (53 unit + 80 integration) and 16 frontend tests, all green

## Phase 9 — Docker & CI/CD
- [x] Single multi-stage Dockerfile (Angular + API, chiseled non-root runtime)
- [x] docker-compose: `db` for dev; `--profile app` adds one-shot `migrate` + `app`
- [x] `.env.example`
- [x] GitHub Actions: build, format, tests, vulnerability audits, image, migration bundle, Render deploy hook
- [x] Dependabot (NuGet, npm, Docker, Actions)
- [x] Image built and the compose stack verified end to end locally (health, SPA, CSP, login)
- [ ] CI run on GitHub *(needs the repository pushed)*

## Phase 10 — Documentation & deployment
- [x] README (quick start, configuration, tests, migrations)
- [x] Deployment guide: Render + Neon step by step; self-host with Caddy/Cloudflare Tunnel; backup/restore; troubleshooting
- [x] MIT LICENSE
- [ ] App live on Render, smoke test passed *(needs your Neon/Render/GitHub accounts)*

## Phase 11 — Development workflow (Jira-style) *(verified 2026-11-07)*
- [x] Statuses Backlog / To Do / In Progress / In Review / Done / Won't Do; admins move any ticket, developers their own
- [x] Issue types (Bug, Task, Story, Improvement), developer/reporter naming, self-assign
- [x] Migration `DevWorkflow` with data mapping (checked against the local database)
- [x] Board endpoint + Kanban board with drag-and-drop; Jira-style ticket page, list and dashboard
- [x] Tests: backend 152, frontend 20

## Phase 12 — Sign-in experience *(verified 2026-11-07)*
- [x] Redesigned login and register (split layout, password toggle, strength meter)
- [x] Forgot / reset password by email (SMTP via MailKit; logged in development), verified end to end in the browser
- [x] "Continue with GitHub / Google" (OAuth + PKCE); redirect verified; full round trip needs your OAuth apps
- [x] Data-protection keys persisted in PostgreSQL (migration `DataProtectionKeys`)
- [x] Tests: backend 173, frontend 23
- [ ] Real GitHub/Google round trip *(needs OAuth apps registered under your accounts)*
- [ ] Real email delivery *(needs an SMTP provider account)*

## Phase 13 — Demo mode and user guide *(verified 2026-11-09)*
- [x] Demo seed (6 accounts, 4 categories, 17 tickets across every status and type) with periodic reset
- [x] One-click demo sign-in, demo banner with role switch, sign-up closed, demo accounts protected
- [x] In-app Guide (/help), dashboard "Try this" tips, docs/user-guide.md
- [x] Tests: backend 182, frontend 25
- [ ] Public demo live on Render *(needs your accounts; see docs/deployment.md §5)*

## Phase 14 — Dashboard status chart *(verified 2026-11-09)*
- [x] Donut chart "Tickets by status" on the dashboard (replaces the per-status cards; total in the centre), scoped to what the user can see
- [x] Fixed colour per status (validated categorical palette), legend with counts and shares, hover readout, click to list
- [x] Works at phone width; the board is back to columns only

## Phase 15 — Sprints *(verified 2026-11-09)*
- [x] Sprint aggregate (planned → active → completed, one active at a time), ticket sprint moves with history, migration `Sprints`
- [x] Board shows the active sprint (header with dates, days left, progress, goal, Complete sprint); falls back to all tickets
- [x] Backlog page: create/edit/delete, start, complete (move unfinished to a planned sprint or the backlog), drag or menu to plan
- [x] Sprint field on the ticket page; demo data with three sprints; guide and user guide updated
- [x] Tests: backend 201, frontend 32

## Phase 16 — Alignment with project CLAUDE.md *(verified 2026-11-11)*
- [x] Status changes only through `Ticket.ChangeStatus`: sprint planning no longer moves Backlog → To Do
- [x] Every ticket change recorded: demo reset and sprint deletion release tickets via `Ticket.DetachFromDeletedSprint` (also fixes a 500 when a planned sprint held a closed ticket)
- [x] Ticket keys shown as `OT-000042` again
- [x] Confirmation dialog for deactivating users and deleting sprints
- [x] Decided 2026-11-15: categories / sprints / developers / history keep returning plain arrays (see docs/architecture.md §15)
