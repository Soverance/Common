namespace Soverance.Auth.Exceptions;

/// <summary>
/// Thrown when an OAuth provider completes the handshake but returns no usable
/// (present + verified) email address. The account cannot be linked or created
/// because the linker keys on email, so the login is rejected.
/// </summary>
public sealed class OAuthEmailRequiredException : Exception
{
    public string Provider { get; }

    public OAuthEmailRequiredException(string provider)
        : base($"OAuth provider {provider} did not return a verified email address")
    {
        Provider = provider;
    }
}
