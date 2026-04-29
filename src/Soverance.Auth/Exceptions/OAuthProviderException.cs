namespace Soverance.Auth.Exceptions;

public sealed class OAuthProviderException : Exception
{
    public string Provider { get; }
    public int? UpstreamStatusCode { get; }
    public string? UpstreamBody { get; }

    public OAuthProviderException(
        string provider,
        string message,
        int? upstreamStatusCode = null,
        string? upstreamBody = null,
        Exception? inner = null)
        : base(message, inner)
    {
        Provider = provider;
        UpstreamStatusCode = upstreamStatusCode;
        UpstreamBody = upstreamBody;
    }
}
