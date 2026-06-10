# novoAI

**novoAI** is a central, multi-tenant **AI assistant platform**. Business applications
register with novoAI as **Apps** and expose their read-only data tools; novoAI then
runs a tool-calling LLM that answers a user's natural-language question by selecting
the right tools, executing them **back in the registered app under the user's own
bearer token**, and phrasing a grounded answer. Every query, permission gate, tenant
scope, and PII redaction stays inside the owning app — novoAI never holds a copy of
the data.

It is built on **.NET 10** with an ASP.NET Core minimal-API back-end and a Blazor
WebAssembly admin portal, organized as a Clean Architecture solution (`AI.sln`).

> Internally the codebase and database still carry the original `ByteAI` / `ByteArabia`
> names (e.g. the `ByteAIDB` database, the seeded admin account). Those are functional
> identifiers and are left as-is; "novoAI" is the product name.

---

## Table of Contents

- [How it works](#how-it-works)
- [Architecture](#architecture)
- [Apps integration contract](#apps-integration-contract)
- [Token trust](#token-trust)
- [Tech stack](#tech-stack)
- [Project structure](#project-structure)
- [Getting started](#getting-started)
- [Configuration](#configuration)
- [Database & seeding](#database--seeding)
- [API surface](#api-surface)
- [Default account](#default-account)
- [License](#license)

---

## How it works

```
            register (Apps table)
   App  ─────────────────────────────►  novoAI
   (Novologs, ByteMart, …)                 │
                                           │  1. user asks  POST /api/assistant/ask  (Bearer = user's own token)
   user ───────────────────────────────►  │  2. load the app's tool catalog  GET  <App.BaseUrl>/api/assistant-data/tools
                                           │  3. LLM (Ollama) picks read tools (function-calling)
   App  ◄───────────────────────────────  │  4. execute each tool  POST <App.BaseUrl>/api/assistant-data/execute
        (runs the query under the              (the SAME user bearer is forwarded → the app does
         user's identity/tenant/perms)          its own JWT validation, tenancy, and permission checks)
                                           │  5. LLM phrases a grounded answer; a leak guard strips
                                           │     stray IDs / system fields, then returns the answer
   user ◄───────────────────────────────  ▼
```

The assistant owns the orchestration only: tool selection, the multi-step tool loop
(bounded by `MaxToolIterations`), answer phrasing, an anti-leak guard, and a learning
log of confirmed tool plans and unanswered questions. It never owns business data.

Key services (in `Infrastructure/Services/Assistant`): `ToolCatalog` (cached per-app
tool snapshots), `AssistantPlanEngine`, `AppToolsClient` (forwards the user bearer),
`AssistantLearningService`, and `OllamaClient`.

---

## Architecture

Clean Architecture — `Web → Api → Application → Domain`, and `Infrastructure → Application → Domain`.

```
┌──────────────────────────────────────────────────────────────┐
│  Web  (Blazor WASM admin portal)   ◄── manage Apps, users,    │
│   │                                    roles, assistant logs   │
│   ▼  HTTP/JSON + SignalR                                       │
│  Api  (ASP.NET Core minimal API + SignalR /hubs/notifications) │
│   ▼                                                            │
│  Application   ◄── use-cases, services (IAssistantService …)   │
│   ▼                                                            │
│  Domain        ◄── entities (App, User, Role, …), enums        │
│   ▲                                                            │
│  Infrastructure ◄── EF Core, seeders, Ollama client,           │
│                     assistant engine, AppTokenTrust, hubs      │
└──────────────────────────────────────────────────────────────┘
```

---

## Apps integration contract

A registered app must expose three endpoints under `<App.BaseUrl>/api/assistant-data`
(all requiring the caller's bearer):

| Method & path | Purpose |
|---|---|
| `GET  /api/assistant-data/tools` | Return the app's tool descriptors (name, description, domain, JSON parameter schema). |
| `POST /api/assistant-data/execute` | Execute one tool for the authenticated caller. Identity args (`current_user_id`, `tenant_id`) are injected server-side by the app — never accepted from novoAI. |
| `GET  /api/assistant-data/branch-context/{branchId}` | Resolve the scope (e.g. warehouse ids) for a branch-locked caller. |

Apps are rows in the **Apps** table, managed from the admin portal and seeded by
`AppSeeder`:

| Field | Meaning |
|---|---|
| `Code` | Stable identifier sent as `appCode` (e.g. `novologs`, `bytemart`). |
| `BaseUrl` | Where novoAI reaches the app's `/api/assistant-data` surface. |
| `PersonaPrompt` | Short persona used when phrasing answers. |
| `JwtAuthority` | The issuer of the app's user tokens (used to validate inbound bearers — see below). |
| `IsActive` | Only active apps are routable. |

Seeded apps: **ByteMart** (`https://localhost:7050`) and **Novologs**
(`http://localhost:5010`, tokens issued by the Novologs tenant service).

---

## Token trust

novoAI accepts two kinds of bearer at `/api/assistant/*`:

1. **Its own users** — symmetric (HS256) tokens signed with `JwtSettings:Secret`.
2. **A registered app's users** — a token whose `iss` matches an active app's
   `JwtAuthority`. Signing keys are discovered from that authority's OIDC metadata
   (`AppTokenTrust`), then the **same token is forwarded** to the app's
   `/api/assistant-data` endpoints, where the app validates it natively.

### Reachable JWKS sources (standalone deployments)

When novoAI runs **separately** from an app (so the issuer URL embedded in the token
isn't reachable from where novoAI runs — e.g. Novologs issues `iss=http://tenant:8080`,
a docker-internal name), configure a per-issuer JWKS override. The issuer string is
still validated against the app's `JwtAuthority`, but the signing keys are fetched
from a reachable URL (short-TTL cached):

```jsonc
"AppsIntegration": {
  "TokenKeySources": [
    { "Issuer": "http://tenant:8080", "JwksUri": "http://localhost:5001/.well-known/jwks.json" }
  ]
}
```

With no source configured, novoAI falls back to standard OIDC discovery from the
authority.

---

## Tech stack

| Layer | Technology |
|---|---|
| Runtime | .NET 10, C# |
| API | ASP.NET Core minimal APIs, Swagger / OpenAPI, rate limiting |
| Real-time | SignalR (`/hubs/notifications`) |
| LLM | Ollama (default model `qwen2.5:14b`), function/tool calling |
| Front-end | Blazor WebAssembly admin portal |
| Persistence | EF Core, SQL Server / LocalDB |
| Auth | JWT bearer (own HS256 + dynamic per-app issuer trust), code-based permissions |

---

## Project structure

```
novoAI/
├── Api/                  # ASP.NET Core minimal-API host
│   ├── Endpoints/        # Auth, User, Role, Permission, UserLog, Media, Lookup,
│   │                     #   Dashboard, Notification, Assistant, AssistantAdmin, Apps
│   ├── Authorization/    # PermissionPolicyProvider + handler
│   ├── Middleware/        # Global exception + permission middleware
│   └── Program.cs        # migrations + DatabaseSeeder on startup, Swagger, CORS, SignalR
├── Application/          # use-cases, services (IAssistantService, …), DTOs
├── Domain/              # entities (App, User, Role, …), enums
├── Infrastructure/      # EF Core, seeders, OllamaClient, Assistant engine,
│   │                     #   AppTokenTrust, NotificationHub, Configuration/*
│   ├── Identity/AppTokenTrust.cs
│   └── Services/Assistant/  # ToolCatalog, AssistantPlanEngine, AppToolsClient, …
├── Web/                 # Blazor WASM admin portal (Apps, users, assistant logs)
├── Tests/
└── AI.sln
```

---

## Getting started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- SQL Server or LocalDB (the API uses `ByteAIDB` by default)
- [Ollama](https://ollama.com) running locally with the configured model pulled:
  `ollama pull qwen2.5:14b`

### Run

```bash
# API — http://localhost:5060  /  https://localhost:7060  (Swagger at /swagger)
dotnet run --project Api/Api.csproj

# Admin portal (Blazor WASM) — http://localhost:5235  /  https://localhost:7099
dotnet run --project Web/Web.csproj
```

Migrations and the `DatabaseSeeder` run automatically on API startup — no manual
`dotnet ef database update` needed.

---

## Configuration

`Api/appsettings.json` (and `appsettings.*.json`) are **git-ignored** — local dev only.
Production values must come from environment variables (`__` separator) or a secret
store. See [`Api/CONFIGURATION.md`](Api/CONFIGURATION.md).

| Key | Description |
|---|---|
| `ConnectionStrings:DefaultConnection` | EF Core connection string (default DB `ByteAIDB`). |
| `JwtSettings:Secret` / `Issuer` / `Audience` | novoAI's own JWT signing/validation. **Replace `Secret` in production.** |
| `OllamaSettings:BaseUrl` / `Model` | LLM endpoint and model (default `http://localhost:11434`, `qwen2.5:14b`). |
| `OllamaSettings:MaxToolIterations` / `MaxToolResultChars` / `TotalTimeoutSeconds` | Tool-loop and timeout bounds. |
| `AppsIntegration:CatalogCacheSeconds` / `TimeoutSeconds` | Per-app tool-catalog cache + HTTP timeout. |
| `AppsIntegration:TokenKeySources` | Optional per-issuer reachable JWKS overrides (see [Token trust](#token-trust)). |
| `CorsOrigins` | Allowed origins for the Blazor client. |

---

## Database & seeding

On startup the API applies EF Core migrations and runs `DatabaseSeeder`, which
idempotently seeds: lookup data, roles + code-based permissions, the default admin
user, and the registered apps (`AppSeeder` — create-if-missing, so admin edits are
never overwritten).

---

## API surface

All endpoints are grouped under `/api/*`; the assistant requires authentication and is
rate-limited (a 3-second per-user debounce plus the `assistant` rate-limit policy).

**`POST /api/assistant/ask`**

```jsonc
// request (AssistantRequest)
{
  "appCode": "novologs",        // optional; defaults to the oldest active app
  "question": "How many open tasks do I have?",
  "history": [ { "role": "user", "content": "…" }, { "role": "assistant", "content": "…" } ],
  "locale": "en",
  "branchId": null               // when set, hard-locks the answer to that branch's scope
}

// response (AssistantResponse)
{ "answer": "…", "history": [ … ] }
```

`POST /api/assistant/report` stores a snapshot of an answer (with optional feedback)
for the learning log. Admin/queue management lives under `/api/assistant-admin/*`.

Other endpoint groups: `auth`, `users`, `roles`, `permissions`, `user-logs`, `media`,
`lookups`, `dashboard`, `notifications`, and `apps`. Real-time notifications are served
over SignalR at `/hubs/notifications`.

---

## Default account

> Local development only. **Do not use in production.**

| Role | Email | Password |
|---|---|---|
| Administrator | `admin@bytearabia.tech` | `ByteArabia@123!` |

---

## License

Proprietary — © ByteArabia. All rights reserved.
