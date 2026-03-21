# Soverance.Common Shared Libraries

**Date:** 2026-03-21
**Status:** Draft
**Goal:** Extract shared authentication and EF Core conventions into reusable libraries consumed by both soverance.com and Vanalytics via git submodule + project references.

## Context

soverance.com and Vanalytics are two .NET 10 web applications with similar but divergent authentication and data access patterns. Both use BCrypt for password hashing, both have User/Role models, and both use EF Core with SQL Server. However, the implementations differ in specifics (cookie vs JWT auth, int vs Guid PKs, entity roles vs enum roles, inline fluent API vs configuration classes).

This effort unifies these patterns into two shared libraries:
- **Soverance.Auth** — Unified User model, BCrypt helpers, JWT token service, cookie/JWT/API key auth configuration, SAML SSO support
- **Soverance.Data** — Base DbContext with shared entities, EF Core entity configurations, SQL Server retry logic

Both consuming apps reference these via git submodule + `<ProjectReference>`. No NuGet packaging or feeds required.

## Design

### Repo Structure

```
Common/
├── src/
│   ├── Soverance.Auth/
│   │   ├── Models/
│   │   │   ├── User.cs
│   │   │   ├── UserRole.cs
│   │   │   ├── RefreshToken.cs
│   │   │   ├── SamlConfig.cs
│   │   │   └── SamlRoleMapping.cs
│   │   ├── Services/
│   │   │   ├── PasswordHasher.cs
│   │   │   ├── TokenService.cs
│   │   │   ├── AdminSeeder.cs
│   │   │   └── SamlService.cs
│   │   ├── Auth/
│   │   │   └── ApiKeyAuthHandler.cs
│   │   ├── Endpoints/
│   │   │   ├── SamlEndpoints.cs
│   │   │   └── SamlAdminEndpoints.cs
│   │   ├── Extensions/
│   │   │   └── AuthExtensions.cs
│   │   ├── DTOs/
│   │   │   ├── AuthDtos.cs
│   │   │   └── SamlDtos.cs
│   │   └── Soverance.Auth.csproj
│   └── Soverance.Data/
│       ├── SoveranceDbContextBase.cs
│       ├── Configurations/
│       │   ├── UserConfiguration.cs
│       │   ├── RefreshTokenConfiguration.cs
│       │   ├── SamlConfigConfiguration.cs
│       │   └── SamlRoleMappingConfiguration.cs
│       ├── Extensions/
│       │   └── DataExtensions.cs
│       └── Soverance.Data.csproj
└── Soverance.Common.slnx
```

### Soverance.Auth — User Model

Unified model based on Vanalytics' modern conventions:

```csharp
public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; }
    public string Username { get; set; }
    public string? PasswordHash { get; set; }
    public string? ApiKey { get; set; }
    public string? OAuthProvider { get; set; }
    public string? OAuthId { get; set; }
    public UserRole Role { get; set; }
    public bool IsSystemAccount { get; set; }
    public bool IsEnabled { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<RefreshToken> RefreshTokens { get; set; } = [];
}

public enum UserRole { Member, Moderator, Admin }
```

**Design decisions:**
- Guid PK (more portable than int, matches Vanalytics)
- Enum role (simpler than entity many-to-many, sufficient for both apps)
- Email required (added to soverance.com — login changes from username-based to email-based)
- `IsEnabled` from soverance.com (added to Vanalytics)
- `IsSystemAccount` replaces soverance.com's `IsBuiltIn`
- ApiKey, OAuth fields nullable (only used by Vanalytics currently)
- `RefreshTokens` navigation property on User (used by both apps)
- App-specific navigation properties (e.g., Vanalytics' `Characters`) are configured from the entity side only (e.g., `CharacterConfiguration` calls `HasOne<User>()`) — they do not appear on the shared User class

**Role enum mapping from existing data:**
- soverance.com `"Administrator"` → `UserRole.Admin`
- soverance.com `"User"` → `UserRole.Member`
- Vanalytics `Admin`, `Moderator`, `Member` → unchanged

Authorization policies in soverance.com change from `.RequireRole("Administrator")` to `.RequireRole("Admin")` to match the enum's string conversion.

**RefreshToken model** (from Vanalytics):

```csharp
public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; }
    public string Token { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
```

**SAML models** (from soverance.com):

- `SamlConfig` — IdP configuration (EntityId, SSO URL, SLO URL, certificate, SP EntityId, AutoProvision, IsEnabled)
- `SamlRoleMapping` — Maps IdP group IDs to `UserRole` enum values. The `RoleName` column stores the enum string value (e.g., `"Admin"`, `"Member"`). The SAML ACS endpoint parses this to a `UserRole` enum when assigning roles — it no longer queries a separate Role entity.

### Soverance.Auth — Services

**`PasswordHasher`** — Static BCrypt wrapper:
- `HashPassword(string password)` → string
- `VerifyPassword(string password, string hash)` → bool

**`TokenService`** — JWT generation (from Vanalytics, registered as singleton):
- `GenerateAccessToken(User user)` → string (includes Id, Email, Username, Role claims)
- `GenerateRefreshToken()` → string (random 32-byte Base64)
- `GetAccessTokenExpiration()` → DateTimeOffset
- `GetRefreshTokenExpiration()` → DateTimeOffset
- Configured from `Jwt:Secret`, `Jwt:Issuer`, `Jwt:Audience`, `Jwt:AccessTokenExpirationMinutes`, `Jwt:RefreshTokenExpirationDays`

**`AdminSeeder`** — Startup admin seeding (from Vanalytics pattern):
- `SeedAsync(DbContext, email, username, passwordHash, logger)` — Creates admin user if not exists, updates if credentials changed. Sets `Role = UserRole.Admin`, `IsSystemAccount = true`, `IsEnabled = true`.

**`SamlService`** — SAML logic extracted from soverance.com's inline endpoint code:
- `BuildSaml2Configuration(SamlConfig, HttpContext)` → Saml2Configuration
- `GetBaseUrl(HttpContext, IWebHostEnvironment)` → string (HTTPS-aware)
- Used by the SAML endpoint extension methods
- SAML ACS endpoint assigns `UserRole` enum directly by parsing the `SamlRoleMapping.RoleName` string via `Enum.TryParse<UserRole>()`. No longer queries a separate Role entity.

**`ApiKeyAuthHandler`** — Custom authentication handler (from Vanalytics):
- Reads `X-Api-Key` header
- BCrypt-verifies against stored hashed API keys
- Creates ClaimsPrincipal with user claims

### Soverance.Auth — Extension Methods

```csharp
public static class AuthExtensions
{
    // Cookie auth for browser-based SPAs (soverance.com)
    public static AuthenticationBuilder AddSoveranceCookieAuth(
        this IServiceCollection services, IWebHostEnvironment environment);

    // JWT bearer auth for API clients (Vanalytics)
    public static AuthenticationBuilder AddSoveranceJwtAuth(
        this IServiceCollection services, IConfiguration configuration);

    // API key auth scheme (Vanalytics addon)
    public static AuthenticationBuilder AddSoveranceApiKeyAuth(
        this AuthenticationBuilder builder);
}
```

**`AddSoveranceCookieAuth`** configures:
- HttpOnly, SameSite=Lax, 7-day expiration, sliding expiration
- SecurePolicy: SameAsRequest (dev), Always (prod) — `IWebHostEnvironment` is passed from the calling app's startup
- Returns 401 instead of redirect for API requests

**`AddSoveranceJwtAuth`** configures:
- Bearer token validation (issuer, audience, lifetime, signing key)
- Reads from `Jwt:*` configuration section

**`AddSoveranceApiKeyAuth`** adds the custom ApiKey scheme.

### Soverance.Auth — SAML Endpoints

Extension methods that any app can call to add SAML support:

- `MapSamlEndpoints(this IEndpointRouteBuilder)` — Public auth flow:
  - `GET /api/auth/saml/status`
  - `GET /api/auth/saml/login`
  - `POST /api/auth/saml/acs`
  - `GET /api/auth/saml/metadata`

- `MapSamlAdminEndpoints(this IEndpointRouteBuilder)` — Admin configuration:
  - `GET /api/admin/saml`
  - `PUT /api/admin/saml`
  - `POST /api/admin/saml/validate-certificate`

Both use `SoveranceDbContextBase` (resolved via DI) for database access — not a concrete app-specific context. Apps that don't need SAML simply don't call these methods.

### Soverance.Auth — DTOs

**Auth DTOs** (shared across both auth schemes):
- `LoginRequest(string Email, string Password)` — Email-based login for both apps
- `RegisterRequest(string Email, string Username, string Password)`
- `OAuthRequest(string Code, string RedirectUri)`
- `RefreshRequest(string RefreshToken)`
- `AuthResponse(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt)`
- `UserProfileResponse(Guid Id, string Email, string Username, bool HasApiKey, UserRole Role, string? OAuthProvider, DateTimeOffset CreatedAt)`

**SAML DTOs** (from soverance.com):
- `SamlConfigResponse`, `SamlConfigUpdateRequest`, `SamlRoleMappingDto`
- `SamlStatusResponse`, `CertificateValidateRequest`, `CertificateValidateResponse`

### Soverance.Data — Base DbContext

```csharp
public abstract class SoveranceDbContextBase : DbContext
{
    protected SoveranceDbContextBase(DbContextOptions options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<SamlConfig> SamlConfigs => Set<SamlConfig>();
    public DbSet<SamlRoleMapping> SamlRoleMappings => Set<SamlRoleMapping>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Apply shared entity configurations (User, RefreshToken, SamlConfig, SamlRoleMapping)
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SoveranceDbContextBase).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
```

**Derived context pattern:** Each app overrides `OnModelCreating` to apply its own configurations:

```csharp
// soverance.com
public class DatabaseContext : SoveranceDbContextBase
{
    public DatabaseContext(DbContextOptions<DatabaseContext> options) : base(options) { }

    public DbSet<Post> Posts => Set<Post>();
    public DbSet<Category> Categories => Set<Category>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder); // Applies shared configs
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DatabaseContext).Assembly); // App-specific configs
    }
}
```

This ensures shared configurations come from the Soverance.Data assembly and app-specific configurations come from the app's own assembly. Each assembly only scans its own types, preventing cross-contamination.

### Soverance.Data — Entity Configurations

All use `IEntityTypeConfiguration<T>` pattern (matching Vanalytics convention):

**`UserConfiguration`:**
- Guid PK
- Unique indexes on Email, Username
- Unique filtered index on ApiKey (where not null)
- Unique filtered index on OAuthProvider+OAuthId (where not null)
- Role stored as string via `HasConversion<string>()`
- Max lengths: Email(256), Username(64), PasswordHash(256), ApiKey(128), OAuthProvider(32), OAuthId(256)

**`RefreshTokenConfiguration`:**
- Guid PK
- FK to User (cascade delete)
- Index on Token

**`SamlConfigConfiguration`:**
- Int PK (single-row config table)
- Required fields: IdpEntityId, IdpSsoUrl, IdpCertificate, SpEntityId

**`SamlRoleMappingConfiguration`:**
- Int PK
- FK to SamlConfig (cascade delete)
- IdpGroupId required
- RoleName required (stores `UserRole` enum string, e.g., `"Admin"`)

### Soverance.Data — Extension Method

```csharp
public static class DataExtensions
{
    public static IServiceCollection AddSoveranceSqlServer<TContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionStringKey = "DefaultConnection")
        where TContext : SoveranceDbContextBase;
}
```

Configures:
- `UseSqlServer` with the specified connection string
- `EnableRetryOnFailure` (5 retries, 10s max delay)
- Registers `TContext` in DI

Both apps call this instead of configuring EF Core manually:
```csharp
services.AddSoveranceSqlServer<DatabaseContext>(configuration);
services.AddSoveranceSqlServer<VanalyticsDbContext>(configuration);
```

### Soverance.Auth — Package Dependencies

```xml
<PackageReference Include="BCrypt.Net-Next" />
<PackageReference Include="ITfoxtec.Identity.Saml2" />
<PackageReference Include="ITfoxtec.Identity.Saml2.MvcCore" />
<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" />
<PackageReference Include="System.IdentityModel.Tokens.Jwt" />
```

### Soverance.Data — Package Dependencies

```xml
<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" />
<ProjectReference Include="../Soverance.Auth/Soverance.Auth.csproj" />
```

### Git Submodule Integration

Both consuming apps add the Common repo as a submodule:

**soverance.com:**
```
soverance.com/
├── lib/Common/              ← git submodule
├── soverance.com/
│   └── soverance.com.csproj  ← ProjectReference to lib/Common/src/...
└── docker-compose.yml
```

**Vanalytics:**
```
Vanalytics/
├── src/
│   ├── lib/Common/          ← git submodule
│   ├── Vanalytics.Api/
│   │   └── Vanalytics.Api.csproj  ← ProjectReference to lib/Common/src/...
│   ├── Vanalytics.Core/
│   └── Vanalytics.Data/
└── docker-compose.yml
```

**CI/CD:** Both `deploy.yml` workflows add submodule checkout:
```yaml
- name: Checkout repository
  uses: actions/checkout@v4
  with:
    fetch-depth: 0
    submodules: recursive
```

**Dockerfiles:** Submodules are inside the build context directory, so `COPY . .` includes them as long as the submodule is initialized. No Dockerfile changes needed.

### Consuming App Changes — soverance.com

**Files deleted** (replaced by shared library):
- `Models/User.cs`
- `Models/Role.cs`
- `Models/SamlConfig.cs`
- `Models/SamlRoleMapping.cs`
- `Api/SamlEndpoints.cs`
- `Api/Dtos/SamlDtos.cs`

**Files modified:**
- `soverance.com.csproj` — Add ProjectReferences to Soverance.Auth and Soverance.Data; remove BCrypt, ITfoxtec package references
- `Models/DatabaseContext.cs` — Inherit from `SoveranceDbContextBase`; remove User, Role, SamlConfig, SamlRoleMapping DbSets and their fluent config from `OnModelCreating`; override `OnModelCreating` to call base then apply app-specific configs; keep app-specific entities (Post, Category); migrate from inline fluent API to `IEntityTypeConfiguration` classes for Post and Category
- `Startup.cs` — Use `AddSoveranceCookieAuth(Environment)` and `AddSoveranceSqlServer<DatabaseContext>(Configuration)`; replace inline `MapSamlApi()` with shared `MapSamlEndpoints()` and `MapSamlAdminEndpoints()`; update `.RequireRole("Administrator")` to `.RequireRole("Admin")` across all endpoints
- `Program.cs` — Use shared `AdminSeeder.SeedAsync()`; remove inline seeding logic and `SeedDefaultAdmin()` method; remove `EnsureMigrationHistory()` method (handled by standard EF migrations on the new schema)
- `Api/AuthEndpoints.cs` — Use `PasswordHasher` instead of direct BCrypt calls; change login from username-based to email-based (use shared `LoginRequest`); update to use shared User model (Guid Id, enum Role); remove `switch-role` endpoint (no longer applicable with single enum role); update `UserResponse` DTO to return single role instead of role list
- `Api/Dtos/AuthDtos.cs` — Update `LoginRequest` to use `Email` instead of `Username`; update `UserResponse` to use `UserRole Role` instead of `List<string> Roles` and `string? ActiveRole`; remove `SwitchRoleRequest`
- `Api/AdminEndpoints.cs` — Update to use shared User model (Guid Id, enum Role); remove `/api/admin/roles` endpoint (Role entity no longer exists); update user role assignment to use `UserRole` enum directly instead of Role entities
- `Api/Dtos/AdminDtos.cs` — Update `UserDetailDto` to use `Guid Id`, `UserRole Role`, `bool IsSystemAccount`; remove role-list DTOs
- `appsettings.template.json` — Connection string key `"Database"` → `"DefaultConnection"`
- `docker-compose.yml` — Env var `ConnectionStrings__Database` → `ConnectionStrings__DefaultConnection`; add `ADMIN_EMAIL` env var
- `.github/workflows/deploy.yml` — Add `submodules: recursive` to checkout; add `ADMIN_EMAIL` env var to Container App update

**Frontend changes** (soverance.com React SPA):
- Login page: change from username field to email field
- Admin user management: update to show single role instead of role list; remove role assignment UI that uses Role entities
- Settings page: remove role-switching UI
- API client types: update to match new DTO shapes (Guid Id, single Role, Email)

**EF Core migration:** A single migration using `migrationBuilder.Sql()` for data transforms that EF Core cannot auto-generate:

1. Create new User table with Guid PK and new schema
2. Migrate existing user data: generate Guid, map `"Administrator"` → `"Admin"`, map `"User"` → `"Member"`, populate Email (use Username as Email for the single existing user, or a configured value)
3. Drop old User table, Role table, and UserRole join table
4. Rename new User table to `Users`
5. Add RefreshToken table (if not already present)
6. Update SamlConfig and SamlRoleMapping tables if schema differs
7. Add CreatedAt, UpdatedAt columns with defaults

Since there's only one user in the production database, the data transforms are straightforward. The migration uses `migrationBuilder.Sql()` blocks for the PK type change and data mapping, which EF Core's auto-scaffolder cannot generate.

### Consuming App Changes — Vanalytics

**Files deleted** (replaced by shared library):
- `Vanalytics.Core/Models/User.cs`
- `Vanalytics.Core/Models/RefreshToken.cs`
- `Vanalytics.Core/Enums/UserRole.cs`
- `Vanalytics.Core/DTOs/Auth/` — Auth DTOs replaced by shared DTOs (LoginRequest, RegisterRequest, OAuthRequest, RefreshRequest, AuthResponse, UserProfileResponse)
- `Vanalytics.Data/Configurations/UserConfiguration.cs`
- `Vanalytics.Data/Configurations/RefreshTokenConfiguration.cs`
- `Vanalytics.Data/Seeding/AdminSeeder.cs`
- `Vanalytics.Api/Services/TokenService.cs`
- `Vanalytics.Api/Auth/ApiKeyAuthHandler.cs`

**Files modified:**
- `Vanalytics.Api.csproj` — Add ProjectReferences to Soverance.Auth and Soverance.Data; remove BCrypt, JWT package references
- `Vanalytics.Data.csproj` — Add ProjectReference to Soverance.Data; remove EF Core SqlServer (now comes via Soverance.Data)
- `Vanalytics.Core.csproj` — Remove BCrypt package reference; add ProjectReference to Soverance.Auth (for User model access)
- `Vanalytics.Data/VanalyticsDbContext.cs` — Inherit from `SoveranceDbContextBase`; remove User, RefreshToken DbSets; override `OnModelCreating` to call base then apply Vanalytics-specific configs; configure app-specific relationships to User from the entity side (e.g., `Character.HasOne<User>()`)
- `Vanalytics.Api/Program.cs` — Use `AddSoveranceJwtAuth()`, `AddSoveranceApiKeyAuth()`, `AddSoveranceSqlServer<VanalyticsDbContext>()`; use shared AdminSeeder
- `Vanalytics.Api/Controllers/AuthController.cs` — Use shared PasswordHasher, TokenService, DTOs
- `.github/workflows/deploy.yml` — Add `submodules: recursive` to checkout

**EF Core migration:** Add migration for:
- Add `IsEnabled` column to Users (default true)
- Add `SamlConfig` table
- Add `SamlRoleMapping` table

## Execution Order

1. **Common repo** — Build the shared libraries first. Both projects must compile independently.
2. **Vanalytics** — Integrate second (simpler changes, closer to the shared model already).
3. **soverance.com** — Integrate last (more complex migration, frontend changes).

Each step should be independently deployable. The Common repo is pure library code with no deployment. Vanalytics and soverance.com each get their own migration and can be deployed independently.

## Files Changed Summary

### Common repo (new)
20 files across Soverance.Auth and Soverance.Data (see Repo Structure section above)

### soverance.com
- 6 files deleted
- 11 files modified
- 1 EF migration added
- Frontend changes to login, admin, settings pages and API client types

### Vanalytics
- 9 files deleted
- 7 files modified
- 1 EF migration added

## What This Does NOT Change

- App-specific API endpoints (blog posts, game data)
- Docker Compose local dev workflow (still `docker compose up`)
- CI/CD deployment pattern (still Docker → ACR → Container Apps)
- Terraform infrastructure
- Cloudflare or DNS configuration
