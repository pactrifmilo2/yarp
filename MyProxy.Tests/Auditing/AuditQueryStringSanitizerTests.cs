using Microsoft.AspNetCore.Http;
using MyProxy.Infrastructure.Auditing;

namespace MyProxy.Tests.Auditing;

public class AuditQueryStringSanitizerTests
{
    [Fact]
    public void Sanitize_returns_null_when_query_is_empty()
    {
        var query = new QueryCollection();

        Assert.Null(AuditQueryStringSanitizer.Sanitize(query));
    }

    [Fact]
    public void Sanitize_preserves_normal_values_and_redacts_secrets()
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString(
            "?where=STATUS%3DOPEN&page_size=20&api_key=secret&custom_token=secret");

        var result = AuditQueryStringSanitizer.Sanitize(context.Request.Query);

        Assert.Equal(
            "?api_key=%5BREDACTED%5D&custom_token=%5BREDACTED%5D&page_size=20&where=STATUS%3DOPEN",
            result);
    }

    [Fact]
    public void Sanitize_limits_the_stored_length()
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString($"?where={new string('a', 5000)}");

        var result = AuditQueryStringSanitizer.Sanitize(context.Request.Query);

        Assert.NotNull(result);
        Assert.Equal(AuditQueryStringSanitizer.MaxStoredLength, result.Length);
        Assert.EndsWith("…", result, StringComparison.Ordinal);
    }
}
