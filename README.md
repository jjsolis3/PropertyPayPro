# PropertyPayPro

A small property-management web app for landlords. Tracks properties, tenants,
leases, and rent payments. Built with **ASP.NET Core 9 (Razor Pages)**, **EF Core +
PostgreSQL (Npgsql)**, and **ASP.NET Core Identity**. Ships as a Docker image for
deployment on Coolify or any container host.

## Tech stack

| Layer       | Choice                                          |
|-------------|-------------------------------------------------|
| Runtime     | .NET 9 / ASP.NET Core 9                         |
| UI          | Razor Pages + Bootstrap 5 (CDN)                 |
| ORM         | Entity Framework Core 9                         |
| Database    | PostgreSQL 16 (via `Npgsql.EntityFrameworkCore.PostgreSQL`) |
| Auth        | ASP.NET Core Identity (cookie auth, register/login UI) |
| Container   | Multi-stage Dockerfile (`mcr.microsoft.com/dotnet/aspnet:9.0-noble`) |
| Compose     | `docker-compose.yml` for local + Coolify        |

## Project layout

```
PropertyPayPro/
├── Dockerfile
├── docker-compose.yml
├── PropertyPayPro.sln
└── src/PropertyPayPro/
    ├── PropertyPayPro.csproj
    ├── Program.cs
    ├── appsettings*.json
    ├── Data/ApplicationDbContext.cs
    ├── Models/         (Property, Tenant, Lease, RentPayment)
    ├── Pages/
    │   ├── Properties/  (Index, Create, Edit, Details, Delete)
    │   ├── Tenants/     ( "  )
    │   ├── Leases/      ( "  )
    │   └── Payments/    ( "  )
    └── wwwroot/
```

## Prerequisites (local dev)

- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- Docker (for Postgres)
- `dotnet-ef` CLI: `dotnet tool install --global dotnet-ef`

## Local development

```bash
# 1. Start Postgres only
docker compose up -d db

# 2. Restore + create the first EF migration (one-time)
cd src/PropertyPayPro
dotnet restore
dotnet ef migrations add InitialCreate

# 3. Run the app — it runs Database.Migrate() on startup
dotnet run
```

Open <http://localhost:5000> (or whatever port `dotnet run` reports).
Register the first account at `/Identity/Account/Register` — that's your admin
user.

## Run everything in Docker locally

```bash
# Build the image and start both services
docker compose up --build
```

The app listens on <http://localhost:8080>. The Postgres password defaults to
`changeme`; override with a `.env` file:

```env
POSTGRES_PASSWORD=somethingbetter
```

## Deploying to Coolify

This repo is set up for Coolify's **Dockerfile** or **Docker Compose**
deployment types.

### Option A — Docker Compose (recommended, includes Postgres)

1. In Coolify, create a new **Docker Compose** resource pointing at this
   repository.
2. Set the environment variable `POSTGRES_PASSWORD` in the Coolify UI.
3. Expose port `8080` on the `app` service; Coolify will route a domain to it
   and terminate TLS.
4. Deploy. On first start, `Database.Migrate()` creates all tables (including
   Identity).

### Option B — Dockerfile + managed Postgres

1. Provision a Postgres service in Coolify (or use a managed one).
2. Create an **Application** resource of type **Dockerfile** pointing at this
   repo.
3. Set these env vars in Coolify:
   - `ASPNETCORE_ENVIRONMENT=Production`
   - `ConnectionStrings__DefaultConnection=Host=<host>;Port=5432;Database=<db>;Username=<user>;Password=<pwd>`
4. Expose container port `8080`.
5. Deploy.

> **Note:** The container runs as the default non-root `app` user from the
> Microsoft ASP.NET image. No volume is needed for the app — all state lives in
> Postgres.

## Adding more features later

The MVP covers properties, leases, and rent payments. The four other areas you
mentioned slot in as additional entities + Razor Pages folders, following the
same pattern:

| Feature       | New model            | New pages folder |
|---------------|----------------------|------------------|
| Bill tracking | `Bill`               | `Pages/Bills/`   |
| Repair tracking | `RepairTicket`     | `Pages/Repairs/` |
| Invoices      | `Invoice` + `InvoiceLine` | `Pages/Invoices/` |
| Receipts      | reuse `RentPayment` + PDF export | `Pages/Receipts/` |

After adding a model, run:

```bash
dotnet ef migrations add Add<FeatureName>
```

The startup `Database.Migrate()` call applies it on the next deploy.

## License

MIT.
