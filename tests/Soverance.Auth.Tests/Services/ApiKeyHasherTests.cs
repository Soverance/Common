using Soverance.Auth.Services;
using Xunit;

namespace Soverance.Auth.Tests.Services;

public class ApiKeyHasherTests
{
    [Fact]
    public void Lookup_is_deterministic_and_64_lowercase_hex()
    {
        var a = ApiKeyHasher.Lookup("2bOQU0t3U8odM3GWUwPCeJ+8NdseAB1xjNl/QDtEkiU=");
        var b = ApiKeyHasher.Lookup("2bOQU0t3U8odM3GWUwPCeJ+8NdseAB1xjNl/QDtEkiU=");
        Assert.Equal(a, b);
        Assert.Equal(64, a.Length);
        Assert.Matches("^[0-9a-f]{64}$", a);
    }

    [Fact]
    public void Lookup_differs_for_different_keys()
    {
        Assert.NotEqual(ApiKeyHasher.Lookup("key-one"), ApiKeyHasher.Lookup("key-two"));
    }
}
