# Sugarista — Agent Ramp-Up Guide

## Project Overview
A full-stack POS/cafe system with three main parts:
- **Sugarista** — .NET 8 backend (API + Domain + Infrastructure + Tests)
- **Sugarista-Admin** — React 19 + Vite admin panel for merchants
- **Sugarista-Theme** — React + Alpine.js customer-facing digital menu (RTL, Persian)

## Commands

### Backend (.NET)
```bash
# From project root
cd Sugarista
dotnet build              # build the solution
dotnet run              # run the API (IIS Express / Kestrel)
dotnet test             # run all unit tests
```
- Solution file: `Sugarista/Sugarista.slnx`
- Projects span `Sugarista/src/` (API, Application, Domain, Infrastructure)
- Tests in `Sugarista/tests/` (Application.UnitTests, Domain.UnitTests)
- Migrations live in `Sugarista/src/Sugarista.Infrastructure/Data/Migrations/`

### Admin Panel (React + Vite)
```bash
cd Sugarista-Admin
npm run dev             # start Vite dev server (http://localhost:5173)
npm run build           # Vite production build
npm run lint          # oxlint
npm run preview         # preview production build
```
- Entry: `Sugarista-Admin/src/main.jsx`
- Pages in `Sugarista-Admin/src/pages/`
- Components in `Sugarista-Admin/src/components/`
- API calls via `Sugarista-Admin/src/utils/adminApi.js`

### Theme (Digital Menu — Static with Alpine.js)
```bash
# No build step required; just open index.html or use a local server
cd Sugarista-Theme
# Optional: serve via npx serve or your preferred static server
```
- Entry: `Sugarista-Theme/index.html`
- CSS: `Sugarista-Theme/assets/css/style.css`
- Alpine.js logic: `Sugarista-Theme/assets/js/app.js`
- Config: `Sugarista-Theme/assets/js/config.js` (sets `SUGARISTA_API_BASE_URL`)

### Infrastructure
```bash
cd /Users/arefzangeneh/Desktop/Sugarista-Project
docker compose up -d    # start any defined services (Postgres, etc.)
make                    # show available Makefile targets
```

## Architecture Notes

### Backend
- **CQRS pattern**: Queries/Handlers in `Sugarista.Application/`
- **EF Core** DbContext: `Sugarista.Infrastructure/Data/ApplicationDbContext.cs`
- **Migrations** are code-first with designer files (`*.Designer.cs`)
- **Authorization** via custom `RequirePermissionAttribute` + `PermissionAuthorizationHandler`
- **Constants** defined in `Sugarista.Domain/Constants/` (permissions, role names, accounting codes)

### Admin Panel
- **React 19** with `react-router-dom` v7
- **Vite** for dev/build, **oxlint** for linting
- State managed via React + Context (`Sugarista-Admin/src/context/AdminContext.jsx`)
- Tables use `@tanstack/react-table` with custom CSS (`Sugarista-Admin/src/components/DataTable.css`)

### Theme
- **Alpine.js 3** for interactivity, **Bootstrap 5** (RTL) for layout
- RTL-aware throughout (`dir="rtl"` on `<html>`)
- Persian date handling: `@majidh1/jalalidatepicker` + custom `shamsiDate.js` utilities
- Cart stored in `localStorage`; split-bill logic (by item / percentage / custom amount)
- Mock payment gateway for testing

## Testing

### Backend Unit Tests
```bash
cd Sugarista
dotnet test               # runs all tests in Sugarista/tests/
```
- `Sugarista.Application.UnitTests/GetHealthQueryHandlerTests.cs` — example test
- `Sugarista.Domain.UnitTests/BaseEntityTests.cs` — entity base tests

### Admin Panel Tests
- No test framework configured in `package.json` currently.

### Theme "Tests"
- No automated tests; manual verification via browser recommended.

## Key File Mappings

| Directory | Purpose |
|-----------|---------|
| `Sugarista/src/Sugarista.Api/Controllers/` | API endpoints (35+ controllers) |
| `Sugarista/src/Sugarista.Application/` | CQRS queries/handlers |
| `Sugarista/src/Sugarista.Domain/Entities/` | 50+ domain entities |
| `Sugarista-Admin/src/pages/` | Admin route pages |
| `Sugarista-Admin/src/components/` | Reusable UI components |
| `Sugarista-Theme/assets/js/app.js` | Alpine.js init & menu logic |
| `Sugarista-Theme/assets/css/style.css` | All custom styling |
| `Sugarista-Theme/index.html` | HTML entry point |

## Common Gotchas

- **RTL throughout**: Both admin and theme use `dir="rtl"` / Persian text. `style.css` uses `direction: ltr` only for embedded English (product names, labels). Always test in right-to-left layout.
- **Environment variables**: Copy `.env.example` to `.env` and adjust `SUGARISTA_API_BASE_URL`. The theme defaults to `"/api"`.
- **Migration Designer files**: `*.Designer.cs` files must stay in sync with their `.cs` counterparts. Do not edit designer files manually unless you understand the codegen pipeline.
- **Sugarista-Admin `type: "module"`**: `package.json` has `"type": "module"` — import paths use `.jsx` extensions explicitly.
- **Alpine.js `x-init`**: In the theme, `init()` runs on component mount; beware of double-initialization when hot-reloading.
- **Scrollbar hiding**: Both `style.css` and `DataTable.css` hide scrollbars with `scrollbar-width: none / ::-webkit-scrollbar { display: none }` — be intentional when adding scrollable containers.
- **`env(safe-area-inset-bottom)`**: Used in `body` padding and `.cart-bar` bottom offset; works in modern browsers only.

## Investigate First (if architecture is unclear)
1. Read `README.md` at root
2. Check `Sugarista/Sugarista.slnx` and `Sugarista-Admin/package.json` for entrypoints
3. Read `Sugarista-Theme/index.html` to see how Alpine.js bootstraps
4. Peek at `Sugarista/src/Sugarista.Domain/Entities/` for entity naming conventions
5. Check `Sugarista-Admin/src/utils/adminApi.js` for API base and auth patterns

## Frequently Useful Scripts

```bash
# From project root
cd Sugarista-Admin && npm run dev    # dev server
cd Sugarista-Admin && npm run build  # production build
cd Sugarista                              && dotnet test   # unit tests
```