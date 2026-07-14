---
name: YARP Hardening & Operations
overview: Harden the completed YARP API gateway for production operations — secure the admin portal, scale audit/monitoring queries, add alerting, enrich the monitoring dashboard, and expose health endpoints. All user-facing text stays Vietnamese (no localization layer). Backend logic is TDD; UI-only changes are exempt.
todos:
  - id: phase8
    content: Secure the admin portal + control-plane API with authentication and authorization (TDD). Protect all Blazor pages and control-plane endpoints; add Vietnamese login/sign-out; decide auth mechanism during implementation.
    status: pending
  - id: phase9
    content: Audit retention policy + push monitoring aggregation into SQL for scale (TDD). Background cleanup/archival of old AuditEntries; rewrite MonitoringSummaryQuery to aggregate in the database; add supporting indexes.
    status: pending
  - id: phase10
    content: Alerting on error-rate and latency thresholds (TDD). Configurable thresholds, evaluation service, in-app alerts surfaced in the admin UI; optional email/webhook later.
    status: pending
  - id: phase11
    content: Live auto-refresh monitoring dashboard + richer Chart.js charts (status codes, latency trend, top clients). UI-only, not TDD.
    status: pending
  - id: phase12
    content: Health and readiness endpoints for gateway and admin hosts (TDD). /health/live and /health/ready with DB and proxy-config checks.
    status: pending
isProject: false
---

# YARP API Gateway — Hardening & Operations

Continuation of [yarp_api_gateway_491857ff.plan.md](yarp_api_gateway_491857ff.plan.md). Phases 1–7 (core gateway, auth, rate limiting, audit, control-plane API, Vietnamese admin UI, monitoring dashboard + Prometheus) are complete. The phases below harden security, scale, and operations.

**Language:** All user-facing strings remain Vietnamese. No `IStringLocalizer` or localization layer.

**Testing:** Backend/domain logic uses TDD (xUnit). UI screens are exempt.

## Recommended implementation order

1. Phase 8 — Admin auth (security gap; do first)
2. Phase 9 — Audit retention + SQL aggregation (scale before traffic grows)
3. Phase 10 — Alerting (builds on monitoring)
4. Phase 11 — Live dashboard + richer charts (UI polish)
5. Phase 12 — Health endpoints (ops readiness)

---

## Phase 8 — Secure the admin portal + control-plane API (TDD)

**Problem:** [MyProxy.Admin/Program.cs](../../MyProxy.Admin/Program.cs) maps Razor components and `MapControlPlaneApi()` with no authentication. Anyone who can reach the admin host can manage clients, mint API keys, and read audit logs.

### Tasks

- Decide auth mechanism for internal operators (confirm during phase):
  - Cookie-based login against an `AdminUser` table (bcrypt/PBKDF2 hashed passwords), **or**
  - Windows/Negotiate auth (if on-prem Active Directory is available), **or**
  - Reverse-proxy / header-based SSO trust.
- If using local accounts: add `AdminUser` entity + migration; seed an initial admin user.
- Protect all control-plane endpoints with an authorization policy.
- Require authenticated user on every Blazor page (fallback policy or `[Authorize]`).
- Add Vietnamese login page and sign-out; redirect unauthenticated users.
- Consider roles (e.g. `Quản trị` vs `Chỉ xem`) to gate write operations on clients, routes, and rate limits.

### Tests first

- Control-plane endpoints return 401/403 when unauthenticated or unauthorized.
- Authenticated, authorized requests succeed.
- Password hashing and verification (if local accounts).
- Anti-forgery still enforced on state-changing calls.

### Key files to touch

- `MyProxy.Admin/Program.cs`
- `MyProxy.Admin/ControlPlane/ControlPlaneEndpoints.cs`
- `MyProxy.Domain/` — new `AdminUser` entity (if local accounts)
- `MyProxy.Infrastructure/` — auth services, DbContext
- `MyProxy.Admin/Components/Pages/` — `Login.razor`, layout auth state

---

## Phase 9 — Audit retention + scalable monitoring aggregation (TDD)

**Problem:** `AuditEntries` grows unbounded. [MonitoringSummaryQuery.cs](../../MyProxy.Infrastructure/Monitoring/MonitoringSummaryQuery.cs) loads all rows in the time window into memory (`ToListAsync` then LINQ-to-objects), which will not scale.

### Tasks

- Add configurable retention policy (e.g. keep N days) via `appsettings.json`.
- Implement a background cleanup service (`IHostedService` or scheduled job) that deletes or archives entries older than the cutoff.
- Rewrite `MonitoringSummaryQuery` to aggregate in SQL (`GROUP BY`, window functions) instead of in-memory LINQ.
- Add database indexes supporting time-window and client aggregation queries (e.g. on `AuditEntries.Timestamp`, `AuditEntries.ClientId`).

### Tests first

- Retention service deletes only entries older than the configured cutoff.
- SQL-side aggregation produces identical results to current logic for representative datasets:
  - Total requests, error count, error rate
  - Average and P95 latency
  - Requests-per-minute buckets
  - Top clients, status-code breakdown

### Key files to touch

- `MyProxy.Infrastructure/Monitoring/MonitoringSummaryQuery.cs`
- `MyProxy.Infrastructure/` — new `AuditRetentionService.cs`
- `MyProxy.Infrastructure/Persistence/` — migration for indexes
- `MyProxy.Tests/Monitoring/`

---

## Phase 10 — Alerting on error-rate / latency thresholds (TDD)

### Tasks

- Define configurable thresholds (e.g. error rate %, P95 latency ms) in configuration.
- Implement an evaluation service that checks `MonitoringSummary` on an interval.
- Persist active alerts and surface them in the admin UI (Vietnamese labels).
- Show active alerts on the monitoring dashboard and overview/home page.
- Optional later: email or webhook notification channels.

### Tests first

- Alert fires when a metric crosses its threshold.
- Alert clears when the metric recovers below threshold.
- No duplicate or flapping alerts within a cooldown window.
- Threshold configuration validation (invalid values rejected).

### Key files to touch

- `MyProxy.Domain/` — `Alert`, `AlertThreshold` entities (or config records)
- `MyProxy.Infrastructure/Monitoring/` — `AlertEvaluationService.cs`
- `MyProxy.Admin/Components/Pages/Monitoring.razor`, `Home.razor`
- `MyProxy.Tests/Monitoring/`

---

## Phase 11 — Live monitoring dashboard + richer charts (UI, not TDD)

### Tasks

- Auto-refresh the monitoring dashboard on a configurable interval (with pause control).
- Keep manual "Tải lại" as a fallback.
- Add charts beyond requests-per-minute using existing summary data:
  - Status-code breakdown (doughnut or bar chart)
  - Latency trend — average vs P95 over the window (line chart)
  - Top clients (horizontal bar chart)
- Extend [monitoringCharts.js](../../MyProxy.Admin/wwwroot/monitoringCharts.js) with create/update/destroy for each new chart.
- Keep visuals consistent with the refreshed theme in [app.css](../../MyProxy.Admin/wwwroot/app.css).

### Key files to touch

- `MyProxy.Admin/Components/Pages/Monitoring.razor`
- `MyProxy.Admin/wwwroot/monitoringCharts.js`
- `MyProxy.Admin/wwwroot/app.css`

---

## Phase 12 — Health & readiness endpoints (TDD)

### Tasks

- Add ASP.NET Core health checks to the gateway ([MyProxy/Program.cs](../../MyProxy/Program.cs)) and admin host ([MyProxy.Admin/Program.cs](../../MyProxy.Admin/Program.cs)):
  - **Liveness** — process is up (`/health/live`)
  - **Readiness** — PostgreSQL reachable; proxy config loaded (`/health/ready`)
- Keep endpoints lightweight and separate from the Prometheus `/metrics` scrape endpoint.
- Return appropriate HTTP status codes (200 healthy, 503 unhealthy).

### Tests first

- Readiness reports unhealthy when the database is unavailable.
- Liveness stays healthy independent of database state.
- Readiness fails when proxy config cannot be loaded (gateway only).

### Key files to touch

- `MyProxy/Program.cs`
- `MyProxy.Admin/Program.cs`
- `MyProxy.Tests/` — health endpoint integration tests

---

## Decisions to confirm during implementation

| Phase | Decision |
|-------|----------|
| 8 | Auth mechanism: local accounts vs Windows/AD vs SSO; whether role-based read/write separation is needed |
| 9 | Retention period (days); delete vs archive old audit entries |
| 10 | Notification channels beyond in-app alerts (email, webhook) |
| 12 | Whether admin host needs separate readiness checks beyond DB |

## Out of scope

- Localization layer (`IStringLocalizer`, resource files) — program is Vietnamese-only by decision.
- External self-service developer portal (internal consumers only).
- Grafana provisioning (custom Vietnamese Blazor dashboard already built in Phase 7).
