# Sugarista

Sugarista is a .NET 10 backend built with **Clean Architecture** for a cafe QR menu, admin content management, and easy Docker-based deployment.

## Architecture

```
Sugarista/
├── src/
│   ├── Sugarista.Domain/          # Entities, value objects, domain events, exceptions
│   ├── Sugarista.Application/     # Use cases (CQRS via MediatR), validation, interfaces
│   ├── Sugarista.Infrastructure/  # EF Core, external services, persistence
│   └── Sugarista.Api/             # HTTP API, middleware, composition root
└── tests/
    ├── Sugarista.Domain.UnitTests/
    └── Sugarista.Application.UnitTests/
```

### Dependency rules

| Layer            | Depends on                          |
|------------------|-------------------------------------|
| Domain           | Nothing                             |
| Application      | Domain                              |
| Infrastructure   | Application                         |
| Api              | Application, Infrastructure         |

Business logic lives in **Application** (commands/queries). **Domain** holds core rules and models. **Infrastructure** implements interfaces defined in Application. **Api** wires everything together.

## Tech stack

- .NET 10 LTS
- ASP.NET Core Web API
- Entity Framework Core 10 + PostgreSQL
- MediatR (CQRS)
- FluentValidation
- JWT authentication for the admin area
- Swagger / OpenAPI

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- PostgreSQL (Docker recommended):

```bash
docker run --name sugarista-postgres \
  -e POSTGRES_DB=sugarista \
  -e POSTGRES_USER=sugarista \
  -e POSTGRES_PASSWORD=ChangeMe123! \
  -p 5432:5432 -d postgres:17-alpine
```

Update the connection string in `src/Sugarista.Api/appsettings.Development.json` if needed.

## Getting started

```bash
# Restore and build
dotnet build

# Run the API
dotnet run --project src/Sugarista.Api

# Run tests
dotnet test
```

API endpoints:

- Swagger UI: `https://localhost:7xxx/swagger` (Development)
- Health check: `GET /api/health`
- Public menu: `GET /api/menu`
- Public site settings: `GET /api/site-settings`
- Admin login: `POST /api/auth/login`

## Adding features

1. Add entities in **Domain** (inherit `BaseEntity` or `BaseAuditableEntity`).
2. Add EF configurations in **Infrastructure/Data/Configurations**.
3. Add commands/queries + handlers in **Application**.
4. Add validators with FluentValidation in **Application**.
5. Expose endpoints via controllers in **Api** using MediatR.

Example migration workflow:

```bash
dotnet ef migrations add InitialCreate \
  --project src/Sugarista.Infrastructure \
  --startup-project src/Sugarista.Api

dotnet ef database update \
  --project src/Sugarista.Infrastructure \
  --startup-project src/Sugarista.Api
```

## Seeded admin user

The first startup seeds a default admin account using the `AdminBootstrap` settings.

Default development values:

- Username: `admin`
- Password: `ChangeMe123!`

Override them in environment variables for production:

- `AdminBootstrap__Username`
- `AdminBootstrap__DisplayName`
- `AdminBootstrap__Password`

## Docker deployment

From the workspace root, the full stack can be started with:

```bash
cp .env.example .env
docker compose up --build
```

Services:

- Public QR menu: `http://localhost/`
- Admin panel: `http://localhost/admin/`
- Database manager: `http://localhost/db-admin/`
- API health: `http://localhost/health`

Adminer login values:

- System: `PostgreSQL`
- Server: `postgres` when using Docker Compose, or `localhost` if you connect directly from your machine
- Username: value of `POSTGRES_USER`
- Password: value of `POSTGRES_PASSWORD`
- Database: value of `POSTGRES_DB`

## License

Private project.
