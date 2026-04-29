namespace Soverance.Auth.Models;

/// <summary>
/// Bound from the "OAuth" configuration section.
/// Mutable property classes (not records) because IConfiguration.Bind() works best
/// with settable properties and parameterless constructors.
/// </summary>
public sealed class OAuthOptions
{
    public OAuthProviderOptions Google { get; set; } = new();
    public OAuthProviderOptions Microsoft { get; set; } = new();
}

public sealed class OAuthProviderOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
}
