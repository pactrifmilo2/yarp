using System.Security.Cryptography;
using System.Text;

namespace MyProxy.Infrastructure.Auth;

public interface IApiKeyHasher
{
    string Hash(string apiKey);
}

public sealed class Sha256ApiKeyHasher : IApiKeyHasher
{
    public string Hash(string apiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
