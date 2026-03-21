# Soverance.Common Shared Libraries Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build two shared .NET 10 libraries (Soverance.Auth + Soverance.Data) that both soverance.com and Vanalytics will consume via git submodule + project references.

**Architecture:** Extract and unify auth/data patterns from both apps into a Common repo. Soverance.Auth holds the unified User model, BCrypt helpers, JWT/cookie/API-key auth, SAML SSO, and shared DTOs. Soverance.Data holds the abstract base DbContext with shared entity configurations and SQL Server retry logic. Soverance.Data references Soverance.Auth for model access.

**Tech Stack:** .NET 10, ASP.NET Core, Entity Framework Core 10, SQL Server, BCrypt.Net-Next, ITfoxtec.Identity.Saml2, System.IdentityModel.Tokens.Jwt

**Spec:** `docs/superpowers/specs/2026-03-21-shared-libraries-design.md`

---

### Task 1: Scaffold solution and project files

**Files:**
- Create: `src/Soverance.Auth/Soverance.Auth.csproj`
- Create: `src/Soverance.Data/Soverance.Data.csproj`
- Create: `Soverance.Common.slnx`

- [ ] **Step 1: Create Soverance.Auth.csproj**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="BCrypt.Net-Next" Version="4.0.3" />
    <PackageReference Include="ITfoxtec.Identity.Saml2" Version="4.10.1" />
    <PackageReference Include="ITfoxtec.Identity.Saml2.MvcCore" Version="4.10.1" />
    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.0.5" />
    <PackageReference Include="System.IdentityModel.Tokens.Jwt" Version="8.8.0" />
  </ItemGroup>

</Project>
```

Note: Uses `FrameworkReference` to access `Microsoft.AspNetCore.App` types (HttpContext, IWebHostEnvironment, endpoint routing, authentication) without being a web project itself. This is the standard pattern for class libraries that provide ASP.NET Core middleware or extensions.

- [ ] **Step 2: Create Soverance.Data.csproj**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="10.0.3" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Soverance.Auth\Soverance.Auth.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 3: Create Soverance.Common.slnx**

```xml
<Solution>
  <Project Path="src/Soverance.Auth/Soverance.Auth.csproj" />
  <Project Path="src/Soverance.Data/Soverance.Data.csproj" />
</Solution>
```

- [ ] **Step 4: Verify solution loads**

Run: `cd "C:/Git/soverance/Common" && dotnet restore Soverance.Common.slnx`
Expected: Restore succeeds (no source files yet, but packages download).

- [ ] **Step 5: Commit**

```
feat: scaffold solution with Auth and Data projects
```

---

### Task 2: Auth models

Create the unified User model and related entities. These are extracted from Vanalytics (modern Guid PK pattern) with additions from soverance.com (IsEnabled, SAML models).

**Files:**
- Create: `src/Soverance.Auth/Models/UserRole.cs`
- Create: `src/Soverance.Auth/Models/User.cs`
- Create: `src/Soverance.Auth/Models/RefreshToken.cs`
- Create: `src/Soverance.Auth/Models/SamlConfig.cs`
- Create: `src/Soverance.Auth/Models/SamlRoleMapping.cs`

**Source reference:**
- UserRole: `Vanalytics/src/Vanalytics.Core/Enums/UserRole.cs` (taken as-is)
- User: `Vanalytics/src/Vanalytics.Core/Models/User.cs` (add IsEnabled from soverance.com, remove app-specific nav properties like Characters)
- RefreshToken: `Vanalytics/src/Vanalytics.Core/Models/RefreshToken.cs` (taken as-is)
- SamlConfig: `soverance.com/Models/SamlConfig.cs` (taken as-is)
- SamlRoleMapping: `soverance.com/Models/SamlRoleMapping.cs` (taken as-is)

- [ ] **Step 1: Create UserRole.cs**

```csharp
namespace Soverance.Auth.Models;

public enum UserRole
{
    Member,
    Moderator,
    Admin
}
```

- [ ] **Step 2: Create User.cs**

```csharp
namespace Soverance.Auth.Models;

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }
    public string? ApiKey { get; set; }
    public string? OAuthProvider { get; set; }
    public string? OAuthId { get; set; }
    public UserRole Role { get; set; } = UserRole.Member;
    public bool IsSystemAccount { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public List<RefreshToken> RefreshTokens { get; set; } = [];
}
```

Key differences from Vanalytics `User`: removed `Characters` nav property (app-specific, configured from entity side), added `IsEnabled` (from soverance.com).

- [ ] **Step 3: Create RefreshToken.cs**

```csharp
namespace Soverance.Auth.Models;

public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public bool IsRevoked { get; set; }

    public User User { get; set; } = null!;
}
```

- [ ] **Step 4: Create SamlConfig.cs**

```csharp
namespace Soverance.Auth.Models;

public class SamlConfig
{
    public int SamlConfigId { get; set; }
    public bool IsEnabled { get; set; }
    public string IdpEntityId { get; set; } = null!;
    public string IdpSsoUrl { get; set; } = null!;
    public string? IdpSloUrl { get; set; }
    public string IdpCertificate { get; set; } = null!;
    public string SpEntityId { get; set; } = null!;
    public bool AutoProvision { get; set; }

    public ICollection<SamlRoleMapping> RoleMappings { get; set; } = new List<SamlRoleMapping>();
}
```

- [ ] **Step 5: Create SamlRoleMapping.cs**

```csharp
namespace Soverance.Auth.Models;

public class SamlRoleMapping
{
    public int SamlRoleMappingId { get; set; }
    public int SamlConfigId { get; set; }
    public string IdpGroupId { get; set; } = null!;
    public string RoleName { get; set; } = null!;

    public SamlConfig SamlConfig { get; set; } = null!;
}
```

- [ ] **Step 6: Build to verify models compile**

Run: `cd "C:/Git/soverance/Common" && dotnet build src/Soverance.Auth/Soverance.Auth.csproj`
Expected: Build succeeded.

- [ ] **Step 7: Commit**

```
feat: add shared auth models (User, UserRole, RefreshToken, SamlConfig, SamlRoleMapping)
```

---

### Task 3: Auth DTOs

Shared request/response DTOs used by both apps. Extracted from Vanalytics DTOs (modern pattern) plus SAML DTOs from soverance.com.

**Files:**
- Create: `src/Soverance.Auth/DTOs/AuthDtos.cs`
- Create: `src/Soverance.Auth/DTOs/SamlDtos.cs`

**Source reference:**
- Auth DTOs: `Vanalytics/src/Vanalytics.Core/DTOs/Auth/*.cs` (6 files consolidated into one)
- SAML DTOs: `soverance.com/Api/Dtos/SamlDtos.cs` (taken as-is, namespace changed)

- [ ] **Step 1: Create AuthDtos.cs**

```csharp
using System.ComponentModel.DataAnnotations;
using Soverance.Auth.Models;

namespace Soverance.Auth.DTOs;

public class LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class RegisterRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(3), MaxLength(64)]
    public string Username { get; set; } = string.Empty;

    [Required, MinLength(8), MaxLength(128)]
    public string Password { get; set; } = string.Empty;
}

public class OAuthRequest
{
    [Required]
    public string Code { get; set; } = string.Empty;

    [Required]
    public string RedirectUri { get; set; } = string.Empty;
}

public class RefreshRequest
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}

public class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
}

public class UserProfileResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public bool HasApiKey { get; set; }
    public string Role { get; set; } = string.Empty;
    public string? OAuthProvider { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
```

- [ ] **Step 2: Create SamlDtos.cs**

```csharp
namespace Soverance.Auth.DTOs;

public record SamlRoleMappingDto(string IdpGroupId, string RoleName);

public record SamlConfigResponse(
    bool IsEnabled,
    string? IdpEntityId,
    string? IdpSsoUrl,
    string? IdpSloUrl,
    string? IdpCertificate,
    string? SpEntityId,
    string SpAcsUrl,
    string SpMetadataUrl,
    bool AutoProvision,
    List<SamlRoleMappingDto> RoleMappings);

public record SamlConfigUpdateRequest(
    bool IsEnabled,
    string IdpEntityId,
    string IdpSsoUrl,
    string? IdpSloUrl,
    string IdpCertificate,
    string SpEntityId,
    bool AutoProvision,
    List<SamlRoleMappingDto> RoleMappings);

public record CertificateValidateRequest(string Certificate);

public record CertificateValidateResponse(
    bool Valid,
    string? Subject,
    string? Issuer,
    DateTime? NotBefore,
    DateTime? NotAfter,
    string? Error);

public record SamlStatusResponse(bool SamlEnabled);
```

- [ ] **Step 3: Build to verify**

Run: `cd "C:/Git/soverance/Common" && dotnet build src/Soverance.Auth/Soverance.Auth.csproj`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```
feat: add shared auth and SAML DTOs
```

---

### Task 4: Auth services (PasswordHasher, TokenService, AdminSeeder)

**Files:**
- Create: `src/Soverance.Auth/Services/PasswordHasher.cs`
- Create: `src/Soverance.Auth/Services/TokenService.cs`
- Create: `src/Soverance.Auth/Services/AdminSeeder.cs`

**Source reference:**
- TokenService: `Vanalytics/src/Vanalytics.Api/Services/TokenService.cs` (namespace change only)
- AdminSeeder: `Vanalytics/src/Vanalytics.Data/Seeding/AdminSeeder.cs` (change DbContext param from `VanalyticsDbContext` to `DbContext`, add `IsEnabled = true`)

- [ ] **Step 1: Create PasswordHasher.cs**

```csharp
namespace Soverance.Auth.Services;

public static class PasswordHasher
{
    public static string HashPassword(string password)
        => BCrypt.Net.BCrypt.HashPassword(password);

    public static bool VerifyPassword(string password, string hash)
        => BCrypt.Net.BCrypt.Verify(password, hash);
}
```

- [ ] **Step 2: Create TokenService.cs**

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Soverance.Auth.Models;

namespace Soverance.Auth.Services;

public class TokenService
{
    private readonly IConfiguration _config;

    public TokenService(IConfiguration config)
    {
        _config = config;
    }

    public string GenerateAccessToken(User user)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_config["Jwt:Secret"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role.ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: GetAccessTokenExpiration().UtcDateTime,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes);
    }

    public DateTimeOffset GetAccessTokenExpiration()
    {
        var minutes = int.Parse(_config["Jwt:AccessTokenExpirationMinutes"] ?? "15");
        return DateTimeOffset.UtcNow.AddMinutes(minutes);
    }

    public DateTimeOffset GetRefreshTokenExpiration()
    {
        var days = int.Parse(_config["Jwt:RefreshTokenExpirationDays"] ?? "7");
        return DateTimeOffset.UtcNow.AddDays(days);
    }
}
```

- [ ] **Step 3: Create AdminSeeder.cs**

The key change from Vanalytics' version: accepts `DbContext` instead of `VanalyticsDbContext` so it works with any derived context. Also sets `IsEnabled = true`.

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Soverance.Auth.Models;

namespace Soverance.Auth.Services;

public static class AdminSeeder
{
    public static async Task SeedAsync(
        DbContext db,
        string email,
        string username,
        string passwordHash,
        ILogger logger)
    {
        var existing = await db.Set<User>().FirstOrDefaultAsync(u => u.Email == email);
        if (existing is not null)
        {
            var changed = false;
            if (existing.Role != UserRole.Admin) { existing.Role = UserRole.Admin; changed = true; }
            if (!existing.IsSystemAccount) { existing.IsSystemAccount = true; changed = true; }
            if (!existing.IsEnabled) { existing.IsEnabled = true; changed = true; }
            if (changed)
            {
                existing.UpdatedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync();
                logger.LogInformation("System admin account updated: {Username}", existing.Username);
            }
            return;
        }

        var admin = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Username = username,
            PasswordHash = passwordHash,
            Role = UserRole.Admin,
            IsSystemAccount = true,
            IsEnabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.Set<User>().Add(admin);
        await db.SaveChangesAsync();
        logger.LogInformation("Admin user seeded: {Username}", username);
    }
}
```

- [ ] **Step 4: Build to verify**

Run: `cd "C:/Git/soverance/Common" && dotnet build src/Soverance.Auth/Soverance.Auth.csproj`
Expected: Build succeeded.

- [ ] **Step 5: Commit**

```
feat: add PasswordHasher, TokenService, and AdminSeeder services
```

---

### Task 5: Data layer (base DbContext, entity configurations, DataExtensions)

**Files:**
- Create: `src/Soverance.Data/SoveranceDbContextBase.cs`
- Create: `src/Soverance.Data/Configurations/UserConfiguration.cs`
- Create: `src/Soverance.Data/Configurations/RefreshTokenConfiguration.cs`
- Create: `src/Soverance.Data/Configurations/SamlConfigConfiguration.cs`
- Create: `src/Soverance.Data/Configurations/SamlRoleMappingConfiguration.cs`
- Create: `src/Soverance.Data/Extensions/DataExtensions.cs`

**Source reference:**
- UserConfiguration: `Vanalytics/src/Vanalytics.Data/Configurations/UserConfiguration.cs` (namespace change, update imports)
- RefreshTokenConfiguration: `Vanalytics/src/Vanalytics.Data/Configurations/RefreshTokenConfiguration.cs` (namespace change)
- SamlConfig/SamlRoleMapping configs: Extracted from `soverance.com/Models/DatabaseContext.cs` inline fluent API, converted to `IEntityTypeConfiguration<T>` classes

- [ ] **Step 1: Create SoveranceDbContextBase.cs**

```csharp
using Microsoft.EntityFrameworkCore;
using Soverance.Auth.Models;

namespace Soverance.Data;

public abstract class SoveranceDbContextBase : DbContext
{
    protected SoveranceDbContextBase(DbContextOptions options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<SamlConfig> SamlConfigs => Set<SamlConfig>();
    public DbSet<SamlRoleMapping> SamlRoleMappings => Set<SamlRoleMapping>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SoveranceDbContextBase).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
```

- [ ] **Step 2: Create UserConfiguration.cs**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Soverance.Auth.Models;

namespace Soverance.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);
        builder.HasIndex(u => u.Email).IsUnique();
        builder.HasIndex(u => u.Username).IsUnique();
        builder.HasIndex(u => u.ApiKey).IsUnique().HasFilter("[ApiKey] IS NOT NULL");
        builder.HasIndex(u => new { u.OAuthProvider, u.OAuthId })
            .IsUnique()
            .HasFilter("[OAuthProvider] IS NOT NULL AND [OAuthId] IS NOT NULL");

        builder.Property(u => u.Email).HasMaxLength(256).IsRequired();
        builder.Property(u => u.Username).HasMaxLength(64).IsRequired();
        builder.Property(u => u.PasswordHash).HasMaxLength(256);
        builder.Property(u => u.ApiKey).HasMaxLength(128);
        builder.Property(u => u.OAuthProvider).HasMaxLength(32);
        builder.Property(u => u.OAuthId).HasMaxLength(256);
        builder.Property(u => u.Role)
            .HasConversion<string>()
            .HasMaxLength(16)
            .HasDefaultValue(UserRole.Member);
    }
}
```

- [ ] **Step 3: Create RefreshTokenConfiguration.cs**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Soverance.Auth.Models;

namespace Soverance.Data.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.HasKey(t => t.Id);
        builder.HasIndex(t => t.Token).IsUnique();

        builder.Property(t => t.Token).HasMaxLength(256).IsRequired();

        builder.HasOne(t => t.User)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

- [ ] **Step 4: Create SamlConfigConfiguration.cs**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Soverance.Auth.Models;

namespace Soverance.Data.Configurations;

public class SamlConfigConfiguration : IEntityTypeConfiguration<SamlConfig>
{
    public void Configure(EntityTypeBuilder<SamlConfig> builder)
    {
        builder.HasKey(e => e.SamlConfigId);

        builder.Property(e => e.IdpEntityId).IsRequired().HasMaxLength(500);
        builder.Property(e => e.IdpSsoUrl).IsRequired().HasMaxLength(500);
        builder.Property(e => e.IdpSloUrl).HasMaxLength(500);
        builder.Property(e => e.IdpCertificate).IsRequired();
        builder.Property(e => e.SpEntityId).IsRequired().HasMaxLength(500);
    }
}
```

- [ ] **Step 5: Create SamlRoleMappingConfiguration.cs**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Soverance.Auth.Models;

namespace Soverance.Data.Configurations;

public class SamlRoleMappingConfiguration : IEntityTypeConfiguration<SamlRoleMapping>
{
    public void Configure(EntityTypeBuilder<SamlRoleMapping> builder)
    {
        builder.HasKey(e => e.SamlRoleMappingId);

        builder.Property(e => e.IdpGroupId).IsRequired().HasMaxLength(100);
        builder.Property(e => e.RoleName).IsRequired().HasMaxLength(100);

        builder.HasOne(e => e.SamlConfig)
            .WithMany(e => e.RoleMappings)
            .HasForeignKey(e => e.SamlConfigId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

- [ ] **Step 6: Create DataExtensions.cs**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Soverance.Data.Extensions;

public static class DataExtensions
{
    public static IServiceCollection AddSoveranceSqlServer<TContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionStringKey = "DefaultConnection")
        where TContext : SoveranceDbContextBase
    {
        services.AddDbContext<TContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString(connectionStringKey),
                sqlOptions => sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorNumbersToAdd: null)));

        return services;
    }
}
```

- [ ] **Step 7: Build both projects**

Run: `cd "C:/Git/soverance/Common" && dotnet build Soverance.Common.slnx`
Expected: Build succeeded for both Soverance.Auth and Soverance.Data.

- [ ] **Step 8: Commit**

```
feat: add base DbContext, entity configurations, and SQL Server extension
```

---

### Task 6: ApiKeyAuthHandler and AuthExtensions

**Files:**
- Create: `src/Soverance.Auth/Auth/ApiKeyAuthHandler.cs`
- Create: `src/Soverance.Auth/Extensions/AuthExtensions.cs`

**Source reference:**
- ApiKeyAuthHandler: `Vanalytics/src/Vanalytics.Api/Auth/ApiKeyAuthHandler.cs` (change `VanalyticsDbContext` to `SoveranceDbContextBase`)
- AuthExtensions: New file combining cookie auth config from `soverance.com/Startup.cs`, JWT config from `Vanalytics/Program.cs`, and API key scheme registration

**Important:** ApiKeyAuthHandler needs to reference `SoveranceDbContextBase` from Soverance.Data, but Soverance.Auth does NOT reference Soverance.Data (the dependency goes the other way). So ApiKeyAuthHandler must use `DbContext` (the EF Core base class) and query `Set<User>()` directly — same pattern as AdminSeeder.

- [ ] **Step 1: Create ApiKeyAuthHandler.cs**

Resolves `DbContext` from the request service provider at request time (registered by `AddSoveranceSqlServer<T>()` in DataExtensions).

```csharp
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Soverance.Auth.Models;

namespace Soverance.Auth.Auth;

public class ApiKeyAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public ApiKeyAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Api-Key", out var apiKeyHeader))
            return AuthenticateResult.NoResult();

        var apiKey = apiKeyHeader.ToString();
        if (string.IsNullOrEmpty(apiKey))
            return AuthenticateResult.NoResult();

        // Resolve the app's registered DbContext from the request scope.
        // Works because both apps register their derived SoveranceDbContextBase via AddSoveranceSqlServer<T>(),
        // which also registers it as DbContext.
        var db = Context.RequestServices.GetRequiredService<DbContext>();

        var usersWithKeys = await db.Set<User>()
            .Where(u => u.ApiKey != null)
            .ToListAsync();

        var user = usersWithKeys.FirstOrDefault(u =>
            BCrypt.Net.BCrypt.Verify(apiKey, u.ApiKey));
        if (user is null)
            return AuthenticateResult.Fail("Invalid API key");

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.Username)
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }
}
```

**Update to DataExtensions.cs (Task 5):** Add `DbContext` registration so ApiKeyAuthHandler and AdminSeeder can resolve it:

In `DataExtensions.cs`, after the `AddDbContext<TContext>` call, add:
```csharp
services.AddScoped<DbContext>(sp => sp.GetRequiredService<TContext>());
```

This maps `DbContext` to whatever concrete context the app registered.

- [ ] **Step 2: Update DataExtensions.cs to register DbContext base**

In `src/Soverance.Data/Extensions/DataExtensions.cs`, the method body becomes:

```csharp
public static IServiceCollection AddSoveranceSqlServer<TContext>(
    this IServiceCollection services,
    IConfiguration configuration,
    string connectionStringKey = "DefaultConnection")
    where TContext : SoveranceDbContextBase
{
    services.AddDbContext<TContext>(options =>
        options.UseSqlServer(
            configuration.GetConnectionString(connectionStringKey),
            sqlOptions => sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null)));

    // Register as DbContext so shared services (ApiKeyAuthHandler, AdminSeeder)
    // can resolve it without knowing the concrete type.
    services.AddScoped<DbContext>(sp => sp.GetRequiredService<TContext>());

    return services;
}
```

- [ ] **Step 3: Create AuthExtensions.cs**

```csharp
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Soverance.Auth.Auth;
using Soverance.Auth.Services;

namespace Soverance.Auth.Extensions;

public static class AuthExtensions
{
    /// <summary>
    /// Cookie auth for browser-based SPAs (soverance.com pattern).
    /// </summary>
    public static AuthenticationBuilder AddSoveranceCookieAuth(
        this IServiceCollection services,
        IWebHostEnvironment environment)
    {
        return services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = environment.IsDevelopment()
                    ? CookieSecurePolicy.SameAsRequest
                    : CookieSecurePolicy.Always;
                options.ExpireTimeSpan = TimeSpan.FromDays(7);
                options.SlidingExpiration = true;
                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = 401;
                    return Task.CompletedTask;
                };
            });
    }

    /// <summary>
    /// JWT bearer auth for API clients (Vanalytics pattern).
    /// Reads Jwt:Secret, Jwt:Issuer, Jwt:Audience from configuration.
    /// </summary>
    public static AuthenticationBuilder AddSoveranceJwtAuth(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<TokenService>();

        return services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = configuration["Jwt:Issuer"],
                    ValidAudience = configuration["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(configuration["Jwt:Secret"]!))
                };
            });
    }

    /// <summary>
    /// API key auth scheme (Vanalytics addon sync).
    /// Call on the AuthenticationBuilder returned by AddSoveranceJwtAuth or AddSoveranceCookieAuth.
    /// </summary>
    public static AuthenticationBuilder AddSoveranceApiKeyAuth(
        this AuthenticationBuilder builder)
    {
        return builder.AddScheme<AuthenticationSchemeOptions, ApiKeyAuthHandler>("ApiKey", null);
    }
}
```

- [ ] **Step 4: Build to verify**

Run: `cd "C:/Git/soverance/Common" && dotnet build Soverance.Common.slnx`
Expected: Build succeeded.

- [ ] **Step 5: Commit**

```
feat: add ApiKeyAuthHandler and auth extension methods (cookie, JWT, API key)
```

---

### Task 7: SAML support (SamlService, SamlEndpoints, SamlAdminEndpoints)

Extract SAML functionality from `soverance.com/Api/SamlEndpoints.cs` into shared components. Key change: the ACS endpoint uses `Enum.TryParse<UserRole>()` to map roles instead of querying a `Role` entity table.

**Files:**
- Create: `src/Soverance.Auth/Services/SamlService.cs`
- Create: `src/Soverance.Auth/Endpoints/SamlEndpoints.cs`
- Create: `src/Soverance.Auth/Endpoints/SamlAdminEndpoints.cs`

**Source reference:**
- All from `soverance.com/Api/SamlEndpoints.cs`, split into service + two endpoint files
- SAML ACS role mapping changed from `db.Role.Where(...)` to `Enum.TryParse<UserRole>()`

- [ ] **Step 1: Create SamlService.cs**

Extracts `BuildSaml2Configuration` and `GetBaseUrl` from inline endpoint code.

```csharp
using System.Security.Cryptography.X509Certificates;
using ITfoxtec.Identity.Saml2;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Soverance.Auth.Models;

namespace Soverance.Auth.Services;

public static class SamlService
{
    public static Saml2Configuration BuildSaml2Configuration(SamlConfig config, HttpContext httpContext)
    {
        var certBytes = Convert.FromBase64String(config.IdpCertificate.Trim());
        var cert = X509CertificateLoader.LoadCertificate(certBytes);

        var saml2Config = new Saml2Configuration
        {
            Issuer = config.SpEntityId,
            SingleSignOnDestination = new Uri(config.IdpSsoUrl),
            CertificateValidationMode = System.ServiceModel.Security.X509CertificateValidationMode.None
        };
        saml2Config.SignatureValidationCertificates.Add(cert);

        saml2Config.AllowedAudienceUris.Add(config.SpEntityId);
        var trimmed = config.SpEntityId.TrimEnd('/');
        if (trimmed != config.SpEntityId)
            saml2Config.AllowedAudienceUris.Add(trimmed);
        else
            saml2Config.AllowedAudienceUris.Add(config.SpEntityId + "/");

        if (!string.IsNullOrWhiteSpace(config.IdpSloUrl))
        {
            saml2Config.SingleLogoutDestination = new Uri(config.IdpSloUrl);
        }

        return saml2Config;
    }

    public static string GetBaseUrl(HttpContext httpContext)
    {
        var request = httpContext.Request;
        var env = httpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
        var scheme = env.IsDevelopment() ? request.Scheme : "https";
        return $"{scheme}://{request.Host}";
    }
}
```

- [ ] **Step 2: Create SamlEndpoints.cs**

Public SAML auth flow endpoints. The ACS endpoint now uses `Enum.TryParse<UserRole>()` instead of querying a Role entity table.

```csharp
using System.Security.Claims;
using ITfoxtec.Identity.Saml2;
using ITfoxtec.Identity.Saml2.MvcCore;
using ITfoxtec.Identity.Saml2.Schemas;
using ITfoxtec.Identity.Saml2.Schemas.Metadata;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Soverance.Auth.DTOs;
using Soverance.Auth.Models;
using Soverance.Auth.Services;

namespace Soverance.Auth.Endpoints;

public static class SamlEndpoints
{
    private const string NameClaim = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name";
    private const string GroupsClaim = "http://schemas.microsoft.com/ws/2008/06/identity/claims/groups";

    public static void MapSamlEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/auth/saml/status
        app.MapGet("/api/auth/saml/status", async (DbContext db) =>
        {
            var config = await db.Set<SamlConfig>().FirstOrDefaultAsync();
            return Results.Ok(new SamlStatusResponse(config?.IsEnabled == true));
        });

        // GET /api/auth/saml/login
        app.MapGet("/api/auth/saml/login", async (DbContext db, HttpContext httpContext) =>
        {
            var config = await db.Set<SamlConfig>().FirstOrDefaultAsync();
            if (config == null || !config.IsEnabled)
                return Results.Redirect("/login?error=saml_disabled");

            var saml2Config = SamlService.BuildSaml2Configuration(config, httpContext);

            var binding = new Saml2RedirectBinding();
            var authnRequest = new Saml2AuthnRequest(saml2Config)
            {
                AssertionConsumerServiceUrl = new Uri($"{SamlService.GetBaseUrl(httpContext)}/api/auth/saml/acs")
            };

            binding.Bind(authnRequest);
            return Results.Redirect(binding.RedirectLocation.OriginalString);
        });

        // POST /api/auth/saml/acs
        app.MapPost("/api/auth/saml/acs", async (DbContext db, HttpContext httpContext) =>
        {
            var config = await db.Set<SamlConfig>()
                .Include(c => c.RoleMappings)
                .FirstOrDefaultAsync();
            if (config == null || !config.IsEnabled)
                return Results.Redirect("/login?error=saml_disabled");

            try
            {
                var saml2Config = SamlService.BuildSaml2Configuration(config, httpContext);
                var binding = new Saml2PostBinding();
                var saml2AuthnResponse = new Saml2AuthnResponse(saml2Config);

                var genericRequest = httpContext.Request.ToGenericHttpRequest();
                binding.ReadSamlResponse(genericRequest, saml2AuthnResponse);

                if (saml2AuthnResponse.Status != Saml2StatusCodes.Success)
                    return Results.Redirect("/login?error=saml_failed");

                var claimsIdentity = saml2AuthnResponse.ClaimsIdentity;

                var usernameClaim = claimsIdentity.Claims
                    .FirstOrDefault(c => c.Type == NameClaim);

                if (usernameClaim == null || string.IsNullOrWhiteSpace(usernameClaim.Value))
                    return Results.Redirect("/login?error=no_username");

                var username = usernameClaim.Value;

                // Read group GUIDs from assertion
                var groupIds = claimsIdentity.Claims
                    .Where(c => c.Type == GroupsClaim)
                    .Select(c => c.Value)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                // Map IdP groups to UserRole enum via role mappings.
                // RoleName stores enum string (e.g., "Admin", "Member").
                // Use the highest-privilege matched role.
                var mappedRoles = config.RoleMappings
                    .Where(m => groupIds.Contains(m.IdpGroupId))
                    .Select(m => Enum.TryParse<UserRole>(m.RoleName, out var role) ? role : (UserRole?)null)
                    .Where(r => r.HasValue)
                    .Select(r => r!.Value)
                    .ToList();

                var assignedRole = mappedRoles.Count > 0
                    ? mappedRoles.Max()  // Admin > Moderator > Member by enum order
                    : UserRole.Member;

                var user = await db.Set<User>()
                    .FirstOrDefaultAsync(u => u.Username == username);

                if (user == null)
                {
                    if (!config.AutoProvision)
                        return Results.Redirect("/login?error=no_account");

                    user = new User
                    {
                        Id = Guid.NewGuid(),
                        Email = username, // SAML typically provides email as name claim
                        Username = username,
                        IsEnabled = true,
                        Role = assignedRole,
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow
                    };
                    db.Set<User>().Add(user);
                    await db.SaveChangesAsync();
                }
                else
                {
                    // Sync role on every login
                    user.Role = assignedRole;
                    user.UpdatedAt = DateTimeOffset.UtcNow;
                    await db.SaveChangesAsync();
                }

                if (!user.IsEnabled)
                    return Results.Redirect("/login?error=disabled");

                var claims = new List<Claim>
                {
                    new(ClaimTypes.Name, user.Username),
                    new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new(ClaimTypes.Role, user.Role.ToString())
                };

                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(identity);

                await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
                return Results.Redirect("/settings");
            }
            catch
            {
                return Results.Redirect("/login?error=saml_failed");
            }
        }).DisableAntiforgery();

        // GET /api/auth/saml/metadata
        app.MapGet("/api/auth/saml/metadata", async (DbContext db, HttpContext httpContext) =>
        {
            var config = await db.Set<SamlConfig>().FirstOrDefaultAsync();
            var baseUrl = SamlService.GetBaseUrl(httpContext);

            var spEntityId = config?.SpEntityId ?? $"{baseUrl}/api/auth/saml/metadata";

            var saml2Config = new Saml2Configuration { Issuer = spEntityId };
            var entityDescriptor = new EntityDescriptor(saml2Config);

            entityDescriptor.SPSsoDescriptor = new SPSsoDescriptor
            {
                AuthnRequestsSigned = false,
                WantAssertionsSigned = true,
                AssertionConsumerServices = new[]
                {
                    new AssertionConsumerService
                    {
                        Binding = ProtocolBindings.HttpPost,
                        Location = new Uri($"{baseUrl}/api/auth/saml/acs"),
                        IsDefault = true
                    }
                },
                NameIDFormats = new[] { NameIdentifierFormats.Unspecified }
            };

            var xml = new Saml2Metadata(entityDescriptor).CreateMetadata().ToXml();
            return Results.Content(xml, "application/xml");
        });
    }
}
```

- [ ] **Step 3: Create SamlAdminEndpoints.cs**

```csharp
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Soverance.Auth.DTOs;
using Soverance.Auth.Models;
using Soverance.Auth.Services;

namespace Soverance.Auth.Endpoints;

public static class SamlAdminEndpoints
{
    public static void MapSamlAdminEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/admin/saml
        app.MapGet("/api/admin/saml", async (DbContext db, HttpContext httpContext) =>
        {
            var config = await db.Set<SamlConfig>()
                .Include(c => c.RoleMappings)
                .FirstOrDefaultAsync();
            var baseUrl = SamlService.GetBaseUrl(httpContext);

            if (config == null)
            {
                return Results.Ok(new SamlConfigResponse(
                    false, null, null, null, null, null,
                    $"{baseUrl}/api/auth/saml/acs",
                    $"{baseUrl}/api/auth/saml/metadata",
                    false, new List<SamlRoleMappingDto>()));
            }

            return Results.Ok(new SamlConfigResponse(
                config.IsEnabled,
                config.IdpEntityId,
                config.IdpSsoUrl,
                config.IdpSloUrl,
                config.IdpCertificate,
                config.SpEntityId,
                $"{baseUrl}/api/auth/saml/acs",
                $"{baseUrl}/api/auth/saml/metadata",
                config.AutoProvision,
                config.RoleMappings.Select(m => new SamlRoleMappingDto(m.IdpGroupId, m.RoleName)).ToList()));
        }).RequireAuthorization(policy => policy.RequireRole("Admin"));

        // PUT /api/admin/saml
        app.MapPut("/api/admin/saml", async (DbContext db, SamlConfigUpdateRequest req) =>
        {
            var config = await db.Set<SamlConfig>()
                .Include(c => c.RoleMappings)
                .FirstOrDefaultAsync();

            if (config == null)
            {
                config = new SamlConfig();
                db.Set<SamlConfig>().Add(config);
            }

            config.IsEnabled = req.IsEnabled;
            config.IdpEntityId = req.IdpEntityId;
            config.IdpSsoUrl = req.IdpSsoUrl;
            config.IdpSloUrl = req.IdpSloUrl;
            config.IdpCertificate = req.IdpCertificate;
            config.SpEntityId = req.SpEntityId;
            config.AutoProvision = req.AutoProvision;

            config.RoleMappings.Clear();
            foreach (var mapping in req.RoleMappings)
            {
                config.RoleMappings.Add(new SamlRoleMapping
                {
                    IdpGroupId = mapping.IdpGroupId,
                    RoleName = mapping.RoleName
                });
            }

            await db.SaveChangesAsync();
            return Results.Ok();
        }).RequireAuthorization(policy => policy.RequireRole("Admin"));

        // POST /api/admin/saml/validate-certificate
        app.MapPost("/api/admin/saml/validate-certificate", (CertificateValidateRequest req) =>
        {
            try
            {
                var bytes = Convert.FromBase64String(req.Certificate.Trim());
                var cert = X509CertificateLoader.LoadCertificate(bytes);
                return Results.Ok(new CertificateValidateResponse(
                    true, cert.Subject, cert.Issuer, cert.NotBefore, cert.NotAfter, null));
            }
            catch (Exception ex)
            {
                return Results.Ok(new CertificateValidateResponse(
                    false, null, null, null, null, ex.Message));
            }
        }).RequireAuthorization(policy => policy.RequireRole("Admin"));
    }
}
```

Note: Admin endpoints use `.RequireRole("Admin")` (matching the `UserRole.Admin` enum string), not `"Administrator"` (soverance.com's old value).

- [ ] **Step 4: Build to verify**

Run: `cd "C:/Git/soverance/Common" && dotnet build Soverance.Common.slnx`
Expected: Build succeeded.

- [ ] **Step 5: Commit**

```
feat: add SAML service, auth endpoints, and admin endpoints
```

---

### Task 8: Final build verification and cleanup

- [ ] **Step 1: Clean build of entire solution**

Run: `cd "C:/Git/soverance/Common" && dotnet build Soverance.Common.slnx --no-incremental`
Expected: Build succeeded with 0 errors, 0 warnings (or only expected warnings).

- [ ] **Step 2: Verify file structure matches spec**

Run: `find "C:/Git/soverance/Common/src" -name "*.cs" -o -name "*.csproj" | sort`

Expected output should match the spec's repo structure:
```
src/Soverance.Auth/Auth/ApiKeyAuthHandler.cs
src/Soverance.Auth/DTOs/AuthDtos.cs
src/Soverance.Auth/DTOs/SamlDtos.cs
src/Soverance.Auth/Endpoints/SamlAdminEndpoints.cs
src/Soverance.Auth/Endpoints/SamlEndpoints.cs
src/Soverance.Auth/Extensions/AuthExtensions.cs
src/Soverance.Auth/Models/RefreshToken.cs
src/Soverance.Auth/Models/SamlConfig.cs
src/Soverance.Auth/Models/SamlRoleMapping.cs
src/Soverance.Auth/Models/User.cs
src/Soverance.Auth/Models/UserRole.cs
src/Soverance.Auth/Services/AdminSeeder.cs
src/Soverance.Auth/Services/PasswordHasher.cs
src/Soverance.Auth/Services/SamlService.cs
src/Soverance.Auth/Services/TokenService.cs
src/Soverance.Auth/Soverance.Auth.csproj
src/Soverance.Data/Configurations/RefreshTokenConfiguration.cs
src/Soverance.Data/Configurations/SamlConfigConfiguration.cs
src/Soverance.Data/Configurations/SamlRoleMappingConfiguration.cs
src/Soverance.Data/Configurations/UserConfiguration.cs
src/Soverance.Data/Extensions/DataExtensions.cs
src/Soverance.Data/Soverance.Data.csproj
src/Soverance.Data/SoveranceDbContextBase.cs
```

- [ ] **Step 3: Commit final state if any fixes were needed**

```
fix: resolve build issues from final verification
```
