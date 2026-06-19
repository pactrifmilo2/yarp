using System.Security.Cryptography;

namespace MyProxy.Infrastructure.Auth;

public interface IApiKeyGenerator
{
    string Generate();
}

public sealed class CryptographicApiKeyGenerator : IApiKeyGenerator
{
    private const string Prefix = "mp_";

    public string Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Prefix + Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
