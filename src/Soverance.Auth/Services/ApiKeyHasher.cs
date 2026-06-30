using System.Security.Cryptography;
using System.Text;

namespace Soverance.Auth.Services;

/// <summary>
/// Deterministic lookup hash for API keys. Keys are high-entropy random tokens, so a fast
/// SHA-256 (not BCrypt) is sufficient and enables an indexed O(1) lookup. Used both when a key
/// is generated (KeysController) and on every authentication (ApiKeyUserResolver).
/// </summary>
public static class ApiKeyHasher
{
    public static string Lookup(string rawKey)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(rawKey)));
}
