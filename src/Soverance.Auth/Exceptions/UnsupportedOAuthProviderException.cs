namespace Soverance.Auth.Exceptions;

public sealed class UnsupportedOAuthProviderException : Exception
{
    public string Provider { get; }

    public UnsupportedOAuthProviderException(string provider)
        : base($"Unsupported OAuth provider: {provider}")
    {
        Provider = provider;
    }
}
