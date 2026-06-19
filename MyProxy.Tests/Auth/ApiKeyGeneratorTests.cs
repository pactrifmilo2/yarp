using MyProxy.Infrastructure.Auth;

namespace MyProxy.Tests.Auth;

public class ApiKeyGeneratorTests
{
    [Fact]
    public void Generate_returns_non_empty_key_with_expected_prefix()
    {
        var generator = new CryptographicApiKeyGenerator();

        var apiKey = generator.Generate();

        Assert.False(string.IsNullOrWhiteSpace(apiKey));
        Assert.StartsWith("mp_", apiKey, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_returns_unique_keys()
    {
        var generator = new CryptographicApiKeyGenerator();

        var keys = Enumerable.Range(0, 100).Select(_ => generator.Generate()).ToHashSet();

        Assert.Equal(100, keys.Count);
    }
}
