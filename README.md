# Soverance.Common

Shared .NET libraries providing authentication, data access, and forum functionality for [Soverance](https://github.com/Soverance) web applications.

## Overview

This repository contains reusable library projects consumed by [soverance.com](https://github.com/Soverance/soverance.com) and [Vanalytics](https://github.com/Soverance/Vanalytics) via git submodule. It provides a unified user model, multiple authentication strategies, a base Entity Framework Core DbContext, and a full-featured forum system.

## Projects

### Soverance.Auth

Authentication and authorization library supporting multiple strategies:

- **JWT Bearer** — Token-based auth for API clients
- **Cookie** — Browser-based session auth for SPAs
- **API Key** — BCrypt-hashed key validation via `X-Api-Key` header
- **SAML SSO** — Full SAML 2.0 integration with IdP configuration, assertion handling, group-to-role mapping, and SP metadata generation

Includes a shared `User` model with role-based authorization (Member, Moderator, Admin), refresh token management, and an admin seeder for initial setup.

### Soverance.Data

Entity Framework Core foundation providing:

- **`SoveranceDbContextBase`** — Abstract base DbContext with shared entity configurations for Users, RefreshTokens, and SAML settings
- **Entity configurations** — Index definitions, field constraints, and value conversions for all shared models
- **SQL Server setup** — Extension method for configuring EF Core with connection retry policies

Consuming applications inherit from `SoveranceDbContextBase` and add their own DbSets and configurations.

### Soverance.Forum

Forum and discussion system with:

- **Categories** — Configurable sections with display ordering and system categories
- **Threads** — Topics with pinning, locking, and cursor-based pagination
- **Posts** — Replies with editing, soft-delete, and audit trails
- **Voting** — One-vote-per-user toggle system
- **Attachments** — File upload tracking with orphan-safe foreign keys
- **Seeder** — Idempotent creation of default system categories

### Soverance.Web

Reusable React TypeScript components, currently providing a SAML admin configuration panel.

## Integration

### As a Git Submodule

```bash
git submodule add https://github.com/Soverance/Common.git lib/Common
```

Then add project references in your `.csproj`:

```xml
<ProjectReference Include="../lib/Common/src/Soverance.Auth/Soverance.Auth.csproj" />
<ProjectReference Include="../lib/Common/src/Soverance.Data/Soverance.Data.csproj" />
<ProjectReference Include="../lib/Common/src/Soverance.Forum/Soverance.Forum.csproj" />
```

### Service Registration

```csharp
// Data access
builder.Services.AddSoveranceSqlServer<MyDbContext>(builder.Configuration, "DefaultConnection");

// Authentication (choose one or combine)
builder.Services.AddSoveranceCookieAuth(builder.Environment);        // Cookie-based
builder.Services.AddSoveranceJwtAuth(builder.Configuration);         // JWT Bearer
builder.Services.AddAuthentication().AddSoveranceApiKeyAuth();       // API Key

// Forum
builder.Services.AddForumServices();
```

### DbContext Inheritance

```csharp
public class MyDbContext : SoveranceDbContextBase
{
    public DbSet<MyEntity> MyEntities { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);  // Applies shared configurations
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MyDbContext).Assembly);
        modelBuilder.ApplyForumConfigurations();  // If using the forum
    }
}
```

### Endpoint Mapping

```csharp
app.MapSamlEndpoints();          // Public SAML login flow
app.MapSamlAdminEndpoints();     // Admin SAML configuration (requires Admin role)
app.MapSamlExchangeEndpoint();   // JWT code exchange for SPA SAML flows
```

## Configuration

### Required Settings

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=...;Database=...;User Id=...;Password=...;TrustServerCertificate=True"
  },
  "Jwt": {
    "Secret": "your-secret-key-at-least-32-bytes",
    "Issuer": "your-issuer",
    "Audience": "your-audience",
    "AccessTokenExpirationMinutes": 15,
    "RefreshTokenExpirationDays": 7
  }
}
```

### Admin Seeding

The `AdminSeeder` creates or syncs a system admin account on startup. Provide credentials via environment variables:

- `ADMIN_EMAIL`
- `ADMIN_USERNAME`
- `ADMIN_PASSWORD`

## Requirements

- .NET 10.0
- SQL Server (local or Azure SQL)

## License

[MIT](LICENSE) - Copyright (c) 2026 Soverance Studios
