# ByteMart

**ByteMart** is a multi-branch retail management platform with a built-in Point-of-Sale, inventory, procurement, promotions, and an admin/approval workflow layer. It is built on .NET 10 with a Blazor WebAssembly front-end and an ASP.NET Core minimal-API back-end, organized as a Clean Architecture solution. The cashier portal is **offline-first** and continues to operate without network connectivity.

---

## Table of Contents

- [Features](#features)
- [Architecture](#architecture)
- [Tech Stack](#tech-stack)
- [Project Structure](#project-structure)
- [Getting Started](#getting-started)
- [Configuration](#configuration)
- [Database & Seeding](#database--seeding)
- [Default Accounts](#default-accounts)
- [Authentication & Permissions](#authentication--permissions)
- [Offline Cashier](#offline-cashier)
- [Localization](#localization)
- [Useful Commands](#useful-commands)
- [Troubleshooting](#troubleshooting)

---

## Features

### Admin Portal
- **Sales** — Orders (POS & online), Promotions (date-bounded, by unit/category/all), Shift management.
- **Catalog** — Products, Units (sellable variants with their own barcodes/prices/costs), Categories.
- **Inventory** — Goods Receiving Notes (GRN), Stock Transfers (between warehouses), Stock Adjustments (Damage/Loss/Theft/Expiry/Corrections), Stock Balances, and an immutable Inventory History ledger.
- **Procurement** — Suppliers with per-unit barcodes.
- **Facilities** — Branches, Warehouses (central or branch-bound), Terminals (POS hardware: computer/printer/payment-machine IPs).
- **Identity** — Users, Roles, Cashiers, and code-based Permissions.
- **Governance** — Requests/approvals for sensitive operations (price changes, product/unit add/update/delete, GRN, stock adjustments/transfers, etc.) with reviewer notes and old/new JSON payloads.
- **Reports & Audit** — User logs, audit trails, dashboards.
- **Real-time Notifications** — SignalR-driven bell for request approvals, rejections, and low-stock alerts.

### Cashier Portal
- Store selector for cashiers assigned to multiple warehouses.
- POS order entry with **Cash / Card / Mobile / Split** payment methods (split tracks separate cash + card amounts).
- 5% VAT broken out per order (configurable per order).
- Full and partial refunds against original order items.
- Shift lifecycle — open shift, cash-in / cash-out, end-of-shift reconciliation, comments.
- **Offline-first** — works without network; orders queue and sync automatically when reconnected.

---

## Architecture

Clean Architecture with five projects:

```
┌─────────────────────────────────────────────────────────────┐
│  Web  (Blazor WASM)        ◄── User-facing client UI       │
│   │                                                         │
│   ├─ HTTP/JSON  ───────────────────────────────────►        │
│   │                                                  ▼      │
│  Api  (ASP.NET Core minimal API + SignalR)                  │
│   │                                                         │
│   ▼                                                         │
│  Application     ◄── Use-cases, DTOs, services, validators │
│   │                                                         │
│   ▼                                                         │
│  Domain          ◄── Entities, enums, value objects        │
│   ▲                                                         │
│   │                                                         │
│  Infrastructure  ◄── EF Core, repositories, seeders, hubs  │
└─────────────────────────────────────────────────────────────┘
```

**Dependency rule**: `Web → Api → Application → Domain` and `Infrastructure → Application → Domain`. Domain has no outward dependencies.

---

## Tech Stack

| Layer | Technology |
|---|---|
| Runtime | .NET 10, C# 14 |
| Front-end | Blazor WebAssembly, Tailwind-style utility CSS, Bootstrap Icons |
| API | ASP.NET Core (minimal APIs), Swagger / OpenAPI |
| Real-time | SignalR (`/hubs/notifications`) |
| Persistence | EF Core, SQL Server / LocalDB |
| Auth | JWT bearer tokens, custom permission-based policy provider |
| Offline storage | IndexedDB (cashier portal cache + queue) |
| Localization | JSON-based localizer, English + Arabic, RTL-aware |

---

## Project Structure

```
ByteMart/
├── Api/                       # ASP.NET Core minimal-API host
│   ├── Endpoints/             # MapXEndpoints per feature
│   ├── Authorization/         # PermissionPolicyProvider + handler
│   ├── Middleware/            # Global exception + permission middleware
│   └── Program.cs
├── Application/               # Use-cases, DTOs, validators
│   ├── Features/              # Auth, Orders, Products, Inventory, Requests, ...
│   ├── Services/
│   └── Validators/
├── Domain/                    # Pure business model
│   ├── Entities/              # Product, Unit, Order, Warehouse, Request, ...
│   ├── Enums/                 # OrderStatus, PaymentMethod, RequestType, ...
│   ├── ValueObjects/
│   ├── Events/
│   └── Repositories/          # Repository interfaces
├── Infrastructure/            # EF Core, migrations, seeders, SignalR hubs
├── Web/                       # Blazor WASM client
│   ├── Components/
│   │   ├── Layout/            # AdminLayout, CashierLayout, PublicLayout
│   │   └── Pages/             # Admin/, Cashier/, Auth/, Public/
│   ├── Services/              # ClientXService — typed API clients
│   ├── Offline/               # IndexedDB, sync, network monitor, offline wrappers
│   ├── Authentication/        # Custom AuthenticationStateProvider
│   ├── Authorization/         # Client-side permission policy provider
│   └── Program.cs
├── Mart.sln
└── README.md
```

---

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- SQL Server, LocalDB, or another EF Core–supported database
- A modern browser (Edge, Chrome, Firefox, Safari)

### Clone, restore, build

```bash
git clone <repo-url>
cd ByteMart
dotnet restore
dotnet build
```

### Run

Open two terminals:

```bash
# Terminal 1 — API (default: https://localhost:5001)
dotnet run --project Api/Api.csproj
```

```bash
# Terminal 2 — Blazor WASM client (default: https://localhost:5002)
dotnet run --project Web/Web.csproj
```

Then open the Blazor URL in your browser and sign in with one of the seeded accounts below.

Swagger UI is available at `<api-base>/swagger`.

---

## Configuration

### API — `Api/appsettings.json` / `appsettings.Development.json`

| Key | Description |
|---|---|
| `ConnectionStrings:DefaultConnection` | EF Core connection string. |
| `JwtSettings:Secret` | Symmetric key used to sign JWTs. **Replace in production.** |
| `JwtSettings:Issuer` / `Audience` / `ExpiryMinutes` | JWT issuance config. |
| `CorsOrigins` | String array of allowed origins for the Blazor client. |

### Web — `Web/wwwroot/appsettings.json`

| Key | Description |
|---|---|
| `ApiBaseUrl` | Base URL of the API (e.g. `https://localhost:5001`). |

Both can be overridden via standard ASP.NET Core environment variables (e.g. `ConnectionStrings__DefaultConnection`).

---

## Database & Seeding

Migrations are applied **automatically on API startup**, and a `DatabaseSeeder` runs idempotently to populate:

- Lookup data (warehouse types, units of measure, etc.)
- Roles + Permissions (Administrator, Cashier, …)
- Default seeded users (see below)
- Sample reference data for development

You do **not** need to run `dotnet ef database update` manually.

To reset the database during development:

```powershell
dotnet ef database drop `
  --project Infrastructure/Infrastructure.csproj `
  --startup-project Api/Api.csproj --force
```

Then restart the API — schema and seed data are recreated on the next launch.

---

## Default Accounts

> Local development only. **Do not use in production.**

| Role | Email | Password |
|---|---|---|
| Administrator | `admin@sma.gov.ae` | `Sma@123!` |
| Cashier | `cashier@sma.gov.ae` | `Sma@123!` |

After signing in, the layout (Admin vs. Cashier) and accessible pages are determined by the user's role and permissions.

---

## Authentication & Permissions

- **JWT bearer** tokens issued by `AuthEndpoints`; refresh tokens supported.
- **Code-based permissions** (`orders.read`, `inventory.write`, `users.read`, …) are attached to Roles and resolved by a custom `PermissionPolicyProvider` on both server and client.
- Blazor pages and nav-menu sections gate visibility through `<AuthorizeView Policy="Permission:xxx">`.
- API endpoints gate access via `[Authorize(Policy = "Permission:xxx")]` or the `PermissionMiddleware`.
- Sensitive write operations (price changes, product CRUD, inventory documents) can be channeled through the **Request approval workflow** rather than executed directly — see `Domain.Enums.RequestType` for the supported request kinds.

---

## Offline Cashier

The cashier portal is designed to operate without connectivity:

1. **Initial sync** — `GET /api/cashier-offline/data` returns the cashier's credentials, profile, assigned stores, products (with units, prices, stock, image URLs + ETags), open shifts, recent orders, and the server clock — all in a single round-trip.
2. **Local cache** — Data is stored in **IndexedDB** via `Web/Offline/IndexedDbService.cs`. Product images are cached separately via `ImageCacheService` (ETag-aware).
3. **Offline-aware service wrappers** — `OfflineOrderService`, `OfflineShiftService`, `OfflineProductService`, `OfflineWarehouseService`, and `OfflineCashierManagementService` decorate the online clients and serve from cache when offline.
4. **Network monitor** — `OfflineNetworkMonitor` reacts to browser online/offline events.
5. **Replay / sync** — Orders and shift events made offline are queued and replayed by `OfflineSyncService` once the connection returns.

---

## Localization

- Strings are stored as JSON resource files and loaded by `JsonStringLocalizerFactory`.
- Supported cultures: **English (en)** and **Arabic (ar)**.
- The selected culture is persisted client-side and applied via `CultureInfo.DefaultThreadCurrentCulture`.
- Layouts are RTL-aware (icons mirror automatically, drawers slide from the correct side).

---

## Useful Commands

| Action | Command |
|---|---|
| Restore | `dotnet restore` |
| Build whole solution | `dotnet build` |
| Run API | `dotnet run --project Api/Api.csproj` |
| Run Web (WASM) | `dotnet run --project Web/Web.csproj` |
| Add migration | `dotnet ef migrations add <Name> --project Infrastructure/Infrastructure.csproj --startup-project Api/Api.csproj` |
| Drop database | `dotnet ef database drop --project Infrastructure/Infrastructure.csproj --startup-project Api/Api.csproj --force` |
| Open Swagger | Browse to `<api-base>/swagger` |

---

## Troubleshooting

- **`PendingModelChangesWarning` on startup** — EF Core detected entity changes that aren't captured in a migration. Startup logs a warning and continues, but the schema is out of date. Create a new migration to resolve it.
- **CORS errors from the browser** — Add the Web client's origin to `CorsOrigins` in the API's `appsettings.json`.
- **401 / 403 in the client** — Check the user has the matching permission for the action (visible under Identity → Roles).
- **Offline cashier shows stale data** — IndexedDB persists across reloads. Use the in-app refresh action, or clear site data in your browser's dev tools.
- **`ApiBaseUrl` not picked up** — Make sure it's set in `Web/wwwroot/appsettings.json` (not `Web/appsettings.json` — the WASM client reads from `wwwroot`).

---

## License

Proprietary — © ByteArabia. All rights reserved.
