using System.Text.RegularExpressions;

namespace MyProxy.Domain.Rules;

public static partial class ScopeValidator
{
    public static bool IsValid(string? scope)
    {
        return scope is not null && ScopePattern().IsMatch(scope.Trim());
    }

    public static string Normalize(string scope)
    {
        var normalized = scope.Trim();

        if (!IsValid(normalized))
        {
            throw new ArgumentException("Scope must use the format action:resource with lowercase letters, numbers, dots, underscores, or hyphens.", nameof(scope));
        }

        return normalized;
    }

    [GeneratedRegex("^[a-z][a-z0-9._-]*:[a-z][a-z0-9._-]*$")]
    private static partial Regex ScopePattern();
}
