---
name: YARP API Gateway
overview: Build an on-prem API gateway on the existing YARP project (MyProxy) with database-driven dynamic config, per-client API-key auth with scopes, per-client rate limiting, audit logging, metrics, and a Vietnamese-language Blazor admin UI for managing clients, monitoring traffic, and viewing audit logs.
todos:
  - id: phase1
    content: Restructure into multi-project solution (Domain/Infrastructure/Admin/Tests); add EF Core + Npgsql + xUnit; define domain entities (Client, Scope, RateLimit, Route, AuditEntry) and GatewayDbContext with initial migration; write domain-rule tests first (TDD).
    status: completed
  - id: phase2
    content: Implement custom IProxyConfigProvider that loads YARP routes/clusters from the database with runtime reload, replacing the static ReverseProxy section in appsettings.json; tests for DB-to-RouteConfig/ClusterConfig mapping and reload.
    status: completed
  - id: phase3
    content: Add API-key authentication middleware (resolve calling client) and scope-based authorization matched to route requirements; TDD valid/invalid/expired keys and scope allow/deny.
    status: pending
  - id: phase4
    content: Implement per-client partitioned rate limiting with limits sourced from DB; TDD enforcement and 429 responses.
    status: pending
  - id: phase5
    content: Add audit-logging middleware capturing timestamp, client, IP, method, endpoint, status, latency to DB; TDD that each request writes a correct audit entry.
    status: pending
  - id: phase6
    content: Build control-plane REST API for client/scope/rate-limit/route CRUD (TDD the API layer) plus a Vietnamese Blazor Server admin UI for client management, audit log viewer, and overview (UI screens not TDD'd).
    status: pending
  - id: phase7
    content: Wire OpenTelemetry metrics, expose Prometheus endpoint, and provision a Grafana monitoring dashboard (or custom Vietnamese dashboard from DB aggregates) matching mockup screen 3.
    status: pending
isProject: false
---

# YARP API Gateway with Vietnamese Admin Portal

## Goal

Extend the existing YARP starter ([MyProxy/Program.cs](MyProxy/Program.cs), currently config-only) into a full internal API gateway matching the mockup: dynamic routing, client/scope management, per-client rate limits, audit log, and monitoring, all driven from a database and managed via a Vietnamese-language admin UI. On-prem, internal consumers only.

## Decision recap (from prior discussion)

- Engine: keep **YARP** (best fit given on-prem + everything-in-Vietnamese + internal consumers).
- Off-the-shelf gateways (Kong/Tyk/APISIX) rejected: English-only admin UIs and external dev portals add no value here.

## Architecture

```mermaid
flowchart LR
  client["Internal API consumer"] -->|API key| gateway
  subgraph gateway [Gateway - ASP.NET Core + YARP]
    authMw["API key + scope middleware"]
    rateMw["Per-client rate limiter"]
    auditMw["Audit middleware"]
    yarp["YARP reverse proxy"]
    authMw --> rateMw --> auditMw --> yarp
  end
  yarp --> backend["Upstream APIs (Flight, NOTAM, Weather...)"]
  configProvider["DB IProxyConfigProvider"] --> yarp
  db[("PostgreSQL")] --- configProvider
  auditMw --> db
  adminUI["Blazor admin UI (Vietnamese)"] --> controlApi["Control-plane API"]
  controlApi --- db
  gateway -->|OpenTelemetry| prometheus["Prometheus"] --> grafana["Grafana"]
```



## Proposed stack

- **.NET 10** (matches existing `net10.0` in [MyProxy/MyProxy.csproj](MyProxy/MyProxy.csproj)).
- **EF Core + PostgreSQL** for clients, scopes, rate limits, routes, audit log.
- **ASP.NET Core built-in rate limiter** (partitioned per client).
- **Serilog** for structured audit + app logging.
- **OpenTelemetry + Prometheus + Grafana** for the monitoring dashboard (screen 3).
- **Blazor Server** for the Vietnamese admin UI (stays in one .NET solution; `IStringLocalizer` resource files for vi-VN).
- **xUnit** test project (TDD for backend/domain logic per your workflow; UI screens exempt).

## Solution layout (target)

- `MyProxy/` - gateway host (proxy + middleware pipeline).
- `MyProxy.Domain/` - entities + business logic (Client, Scope, RateLimit, Route, AuditEntry).
- `MyProxy.Infrastructure/` - EF Core DbContext, repositories, dynamic `IProxyConfigProvider`.
- `MyProxy.Admin/` - Blazor Server admin UI (Vietnamese).
- `MyProxy.Tests/` - xUnit tests.

## Phases

### Phase 1 - Solution + data foundations (TDD)

- Restructure into the multi-project solution above; add  + Npgsql + xUnit.
- Define domain entities and `GatewayDbContext`; create initial migration.
- Tests first for domain rules (scope parsing, rate-limit validation).

### Phase 2 - Dynamic YARP config from DB

- Implement custom `IProxyConfigProvider` loading routes/clusters from the DB, with runtime reload (replaces static `ReverseProxy` section in [MyProxy/appsettings.json](MyProxy/appsettings.json)).
- Tests for config mapping (DB rows -> `RouteConfig`/`ClusterConfig`) and reload signaling.

### Phase 3 - Auth + scopes (TDD)

- API-key authentication middleware resolving the calling client.
- Scope authorization (`read:flights`, `write:flights`, etc.) matched against route requirements.
- Tests for valid/invalid/expired keys and scope allow/deny.

### Phase 4 - Per-client rate limiting (TDD)

- Partitioned rate limiter keyed by client, limits sourced from DB (the `req/min` column in mockup screen 2).
- Tests for limit enforcement and 429 behavior.

### Phase 5 - Audit logging (TDD)

- Middleware capturing timestamp, client, IP, method, endpoint, status, latency -> DB (mockup screen 4).
- Tests asserting an audit entry per request with correct fields.

### Phase 6 - Control-plane API + Vietnamese admin UI

- REST endpoints for client/scope/rate-limit/route CRUD (TDD on the API layer).
- Blazor Server admin UI in Vietnamese: client management (screen 2), audit log viewer (screen 4), overview/docs page (screen 1). UI screens themselves not TDD'd.

### Phase 7 - Monitoring dashboard

- Wire OpenTelemetry metrics from the gateway; expose Prometheus scrape endpoint; provision a Grafana dashboard (screen 3). Optionally embed/iframe into the admin UI, or build a custom Vietnamese dashboard from DB aggregates if pixel-control is required.

## Out of scope / assumptions

- No external self-service developer portal (consumers are internal).
- Swagger/Postman doc hosting (screen 1) treated as static links initially.
- Decisions to confirm during Phase 1: PostgreSQL vs SQL Server; Blazor Server vs a separate React/Vue SPA; Grafana embed vs fully custom Vietnamese monitoring UI.

