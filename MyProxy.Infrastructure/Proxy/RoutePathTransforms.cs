namespace MyProxy.Infrastructure.Proxy;

public static class RoutePathTransforms
{
    private const string CatchAllParameter = "{**catch-all}";

    public static string? TryGetPathRemovePrefix(string path)
    {
        var catchAllIndex = path.IndexOf(CatchAllParameter, StringComparison.Ordinal);
        if (catchAllIndex < 0)
        {
            return null;
        }

        var prefix = path[..catchAllIndex].TrimEnd('/');
        return string.IsNullOrEmpty(prefix) ? null : prefix;
    }

    public static IReadOnlyList<IReadOnlyDictionary<string, string>>? MapTransforms(string path)
    {
        var pathRemovePrefix = TryGetPathRemovePrefix(path);
        if (pathRemovePrefix is null)
        {
            return null;
        }

        return
        [
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["PathRemovePrefix"] = pathRemovePrefix,
            },
        ];
    }
}
