# Soverance.Common

Shared .NET libraries providing authentication, data access, and forum functionality for [Soverance](https://github.com/Soverance) web applications.

## Overview

This repository contains reusable library projects published as NuGet packages and one npm package, consumed by [soverance.com](https://github.com/Soverance/soverance.com), [Vanalytics](https://github.com/Soverance/Vanalytics), and [Foundation](https://github.com/Soverance/Foundation). It provides a unified user model, multiple authentication strategies, a base Entity Framework Core DbContext, a full-featured forum system, and a shared React component + theming library.

## Projects

### Soverance.Auth

Authentication and authorization library supporting multiple strategies:

- **JWT Bearer** — Token-based auth for API clients
- **Cookie** — Browser-based session auth for SPAs
- **API Key** — BCrypt-hashed key validation via `X-Api-Key` header
- **SAML SSO** — Full SAML 2.0 integration with IdP configuration, assertion handling, group-to-role mapping, and SP metadata generation
- **OAuth 2.0** — Google and Microsoft sign-in via SPA-driven code-exchange flow. Auto-links by verified email; rejects logins where an email is already linked to a different OAuth identity.

Includes a shared `User` model with role-based authorization (Member, Moderator, Admin), refresh token management, and an admin seeder for initial setup.

### Soverance.Data

Provider-agnostic EF Core foundation providing:

- **`SoveranceDbContextBase`** — Abstract base DbContext with shared entity configurations for Users, RefreshTokens, and SAML settings. Provider-aware filtered indexes (T-SQL filters applied automatically when running against SQL Server).
- **Entity configurations** — Index definitions, field constraints, and value conversions for all shared models
- **`AddSoveranceDataProtection<TContext>`** — Persists ASP.NET Core Data Protection keys to the database

Provider registration extensions live in companion packages — pick exactly one for your application:

- **`Soverance.Data.SqlServer`** — `AddSoveranceSqlServer<TContext>` (uses `Microsoft.EntityFrameworkCore.SqlServer`)
- **`Soverance.Data.Postgres`** — `AddSoverancePostgres<TContext>` (uses `Npgsql.EntityFrameworkCore.PostgreSQL`)

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

Reusable React TypeScript components and theming for Soverance applications:

- **`<SamlTab>`** — SAML admin configuration panel
- **`<ThemeProvider>` / `<ThemeSwitcher>` / `useTheme()`** — Light, dark, and glass themes with localStorage persistence and runtime switching
- **`themes.css`** — CSS custom-property token blocks for the three themes plus a primary-tinted ambient body gradient
- **`<GlassBackground />`** — Animated R3F particle background for the glass theme, parameterized by branding primary color (lazy-loaded via `@soverance/web/glass` to keep three.js out of light/dark builds)

## Consuming These Packages

All four .NET libraries and the `@soverance/web` npm package ship as published packages — **not** as git submodules or `<ProjectReference>` links.

### .NET (NuGet)

Add the package(s) you need to your `.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="Soverance.Auth" Version="1.1.0" />
  <PackageReference Include="Soverance.Data" Version="1.1.0" />
  <!-- Pick exactly one provider companion: -->
  <PackageReference Include="Soverance.Data.SqlServer" Version="1.0.0" />
  <!-- OR -->
  <PackageReference Include="Soverance.Data.Postgres" Version="1.0.0" />

  <!-- Optional: forum -->
  <PackageReference Include="Soverance.Forum" Version="1.0.8" />
</ItemGroup>
```

`Soverance.Data` 1.1.0 no longer ships the `AddSoveranceSqlServer` extension directly — that extension now lives in `Soverance.Data.SqlServer`. Apps upgrading from `Soverance.Data 1.0.x` to `1.1.0` must add a reference to either `Soverance.Data.SqlServer` or `Soverance.Data.Postgres`. Existing apps using SQL Server: pick `Soverance.Data.SqlServer`. New PostgreSQL apps: pick `Soverance.Data.Postgres`.

### npm

```jsonc
"dependencies": {
  "@soverance/web": "1.1.0"
}
```

If you intend to use the new `<GlassBackground />` (animated R3F particle background for the glass theme), also install the optional peers:

```jsonc
"dependencies": {
  "three": "^0.183.0",
  "@react-three/fiber": "^9.5.0"
}
```

Otherwise these are not required — the default `@soverance/web` import surface (`SamlTab`, `ThemeProvider`, `ThemeSwitcher`, `useTheme`) does not pull in three.js.

Use `<GlassBackground />` via lazy import to keep the three.js bundle out of light/dark theme builds:

```tsx
import { lazy, Suspense } from 'react'
import { useTheme } from '@soverance/web'

const GlassBackground = lazy(() =>
  import('@soverance/web/glass').then(m => ({ default: m.GlassBackground })),
)

function GlassMount() {
  const { theme } = useTheme()
  if (theme !== 'glass') return null
  return <Suspense fallback={null}><GlassBackground /></Suspense>
}
```

### Service Registration

```csharp
// Data access — pick the companion that matches your provider
using Soverance.Data.SqlServer.Extensions;   // or Soverance.Data.Postgres.Extensions
builder.Services.AddSoveranceSqlServer<MyDbContext>(builder.Configuration, "DefaultConnection");
// or
builder.Services.AddSoverancePostgres<MyDbContext>(builder.Configuration, "DefaultConnection");

// Authentication (choose one or combine)
builder.Services.AddSoveranceCookieAuth(builder.Environment);        // Cookie-based
builder.Services.AddSoveranceJwtAuth(builder.Configuration);         // JWT Bearer
builder.Services.AddAuthentication().AddSoveranceApiKeyAuth();       // API Key

// Forum
builder.Services.AddForumServices();

// OAuth (Google + Microsoft)
builder.Services.AddSoveranceOAuth(builder.Configuration);

// Data Protection — provider-agnostic; call after the provider registration above
using Soverance.Data.Extensions;
builder.Services.AddSoveranceDataProtection<MyDbContext>("MyApp");
```

### DbContext Inheritance

```csharp
public class MyDbContext : SoveranceDbContextBase
{
    public MyDbContext(DbContextOptions<MyDbContext> options) : base(options) { }

    public DbSet<MyEntity> MyEntities { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);  // Applies shared configurations + provider-aware filters
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
app.MapOAuthEndpoints();         // Google + Microsoft OAuth login
```

### Migrations

When you bump `Soverance.Data` (e.g., to pick up a new shared entity property), each consuming app must regenerate its own migration:

```sh
dotnet ef migrations add Probe --project src/MyApp.Data --startup-project src/MyApp
```

If `Probe` is empty (no schema changes), delete it (`dotnet ef migrations remove`) and you're done. If it contains operations, those reflect schema changes the bumped Common package introduced — review and apply them.

### OAuth Configuration

```json
{
  "OAuth": {
    "Google":    { "ClientId": "...", "ClientSecret": "..." },
    "Microsoft": { "ClientId": "...", "ClientSecret": "..." }
  }
}
```

The `app.MapOAuthEndpoints()` call registers `POST /api/auth/oauth/{provider}` where `{provider}` is `google` or `microsoft`. The SPA POSTs `{ code, redirectUri }` after handling the IdP redirect itself.

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
- A relational database provider — SQL Server (via `Soverance.Data.SqlServer`) or PostgreSQL (via `Soverance.Data.Postgres`)

## License

[MIT](LICENSE) - Copyright (c) 2026 Soverance Studios
