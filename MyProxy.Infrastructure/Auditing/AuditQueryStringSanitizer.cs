using Microsoft.AspNetCore.Http;

namespace MyProxy.Infrastructure.Auditing;

public static class AuditQueryStringSanitizer
{
    public const int MaxStoredLength = 4096;

    private const string RedactedValue = "[REDACTED]";

    private static readonly HashSet<string> SensitiveNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "api-key",
        "api_key",
        "apikey",
        "authorization",
        "client-secret",
        "client_secret",
        "credential",
        "key",
        "password",
        "passwd",
        "refresh-token",
        "refresh_token",
        "secret",
        "sig",
        "signature",
        "token",
        "access-token",
        "access_token",
    };

    public static string? Sanitize(IQueryCollection query)
    {
        if (query.Count == 0)
        {
            return null;
        }

        var parameters = new List<KeyValuePair<string, string?>>();

        foreach (var parameter in query.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (IsSensitive(parameter.Key))
            {
                parameters.Add(new KeyValuePair<string, string?>(parameter.Key, RedactedValue));
                continue;
            }

            if (parameter.Value.Count == 0)
            {
                parameters.Add(new KeyValuePair<string, string?>(parameter.Key, string.Empty));
                continue;
            }

            foreach (var value in parameter.Value)
            {
                parameters.Add(new KeyValuePair<string, string?>(parameter.Key, value));
            }
        }

        var sanitized = QueryString.Create(parameters).Value;
        if (string.IsNullOrEmpty(sanitized) || sanitized.Length <= MaxStoredLength)
        {
            return sanitized;
        }

        return string.Concat(sanitized.AsSpan(0, MaxStoredLength - 1), "…");
    }

    private static bool IsSensitive(string name)
    {
        if (SensitiveNames.Contains(name))
        {
            return true;
        }

        var normalized = name.Replace('-', '_');
        return normalized.EndsWith("_password", StringComparison.OrdinalIgnoreCase) ||
               normalized.EndsWith("_secret", StringComparison.OrdinalIgnoreCase) ||
               normalized.EndsWith("_signature", StringComparison.OrdinalIgnoreCase) ||
               normalized.EndsWith("_token", StringComparison.OrdinalIgnoreCase);
    }
}
