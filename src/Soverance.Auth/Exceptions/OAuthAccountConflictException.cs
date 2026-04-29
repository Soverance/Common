namespace Soverance.Auth.Exceptions;

public sealed class OAuthAccountConflictException : Exception
{
    public string Email { get; }

    public OAuthAccountConflictException(string email)
        : base($"OAuth account conflict for email: {email}")
    {
        Email = email;
    }
}
