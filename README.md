# Sugarista

## Apply UI changes with Docker

The UI containers now mount your local source files, so changes are picked up correctly when you restart the stack.

### Recommended command (after editing UI files)

```bash
make up
```

Or:

```bash
./scripts/dev-up.sh
```

Both run:

```bash
docker compose up -d --build --force-recreate admin-ui public-ui proxy
```

`--force-recreate` is required for the **admin panel** so the container re-runs its build step.  
`--build` alone may leave old containers running.

### What happens now

| App | Folder | How changes apply |
|-----|--------|-------------------|
| Digital menu | `Sugarista-Theme/` | **Immediate** — files are volume-mounted; refresh the browser |
| Admin panel | `Sugarista-Admin/` | **Auto-rebuild** when you save files (file watcher in container). First load still happens on container start. |

### URLs

- Digital menu: http://localhost/
- Admin panel: http://localhost/admin/

### Full stack

```bash
docker compose up -d --build --force-recreate
```

### Admin-only quick refresh

If auto-rebuild is disabled or you want to force a rebuild immediately:

```bash
make admin-reload
# or
docker compose restart admin-ui
```

Then hard-refresh the browser (`Cmd+Shift+R` on Mac, `Ctrl+Shift+R` on Windows/Linux).

### Local dev without Docker (optional)

**Admin:**

```bash
cd Sugarista-Admin && npm install && npm run dev
```

Open http://localhost:5173/admin/

**Theme:** use volume mounts via Docker, or serve `Sugarista-Theme` with any static file server.

## Technology Stack & Architecture

### Backend (.NET 10)
- **Framework**: ASP.NET Core 10 (Kestrel)
- **Architecture**: Clean Architecture + CQRS (MediatR) — `Domain` / `Application` / `Infrastructure` / `Api`
- **ORM**: Entity Framework Core 10 + Npgsql (PostgreSQL 17)
- **Validation**: FluentValidation
- **Auth**: JWT (stateless admin bootstrap user) + custom `RequirePermissionAttribute` / `PermissionAuthorizationHandler`
- **Background jobs**: `IHostedService` tickers (maintenance reminders, export job runner)
- **Excel export**: ClosedXML
- **Image processing**: SkiaSharp (MIT) — decode validation, EXIF strip, WebP compression, animated pass-through
- **File storage**: `IFileStorageService` / `IImageFileStore` → local disk under `uploads/` (Docker volume `api-uploads`)
- **Notifications**: SMS via Kavenegar (or logging fallback), email via SMTP (or logging fallback)
- **Payments**: mock gateway (configurable provider)

### Frontend
- **Admin panel**: React 19 + Vite, react-router-dom v7, `@tanstack/react-table`, oxlint
- **Customer menu (Theme)**: Alpine.js 3 + Bootstrap 5 (RTL), Persian (Jalali) dates
- **State**: React Context (`AdminContext`)

### Database & Infra
- **PostgreSQL 17** (Docker)
- **Docker Compose** + nginx reverse proxy (client_max_body_size 10m)
- **Static files**: `/api/files` → `uploads/` (immutable cache for `/images/`)

### Key Patterns
- CQRS commands/queries with MediatR
- Repository via `IApplicationDbContext`
- Auditable entities (`BaseAuditableEntity`) + activity log filter
- Permission-based authorization per route
- Schema compatibility seeder (`EnsureSchemaCompatibilityAsync`) for incremental DB updates
- Domain validation centralized in entities (e.g. `MaintenanceRecipientRules`)
- Reusable image upload module: `ImageUploader` (React) + `/api/admin/uploads/images` (SkiaSharp pipeline)

### Modules
- Orders, payments, POS, inventory & purchases, assets registry, HR/payroll/scheduling
- Maintenance (equipment service reminders, SMS/email, cost logs)
- Site settings, loyalty, exports, activity logs, admin users/roles
- Thermal receipt printing (customer + barista copies) via `Sugarista-PrintAgent`

### Thermal Receipt Printing

Every paid order — POS (cash/card), online gateway, and split-bill (on the final share) — enqueues **two print jobs**: a customer receipt (items, totals, payment method, tracking code) and a barista ticket (large order code, items with sizes/add-ons, notes, no prices).

The server only queues rows in `PrintJobs`; the physical printing happens on the MacBook that is USB-connected to the printer:

```
Sugarista API ──► PrintJobs (Pending)
                       │  GET /api/print-agent/pending   (X-Agent-Key)
                       ▼
             Sugarista-PrintAgent (Node.js, macOS)
                       │  render.py → 384-dot PNG (Persian shaping + bidi)
                       ▼
                  lp -d GA-E200I  →  CUPS  →  printer
```

- Agent folder: [`Sugarista-PrintAgent/`](Sugarista-PrintAgent/README.md) — `./install.sh` sets up the venv and a `LaunchAgent` (`com.sugarista.printagent`) that starts at login and restarts on crash.
- Auth: shared key in `X-Agent-Key`, configured via `PRINT_AGENT_KEY` (server `.env`) and `apiKey` (agent `config.json`).
- Jobs are claimed on fetch (`Dispatched`); stale dispatches older than 2 minutes return to `Pending`; the agent retries 3× before the job is marked `Failed`.
- Receipt rendering is wrapped in try/catch — a printer or agent failure never breaks checkout.

### Testing
- `dotnet test` — Domain (xunit) + Application unit tests
- Admin: no test framework configured
- Theme: manual browser verification

### Environment
- Copy `.env.example` → `.env`; key vars: `Sms__Provider`, `Email__Provider`, `Payment__Provider`, `AdminBootstrap__Password`
- Default bootstrap admin: `admin` / `ChangeMe123!` (change in production)
