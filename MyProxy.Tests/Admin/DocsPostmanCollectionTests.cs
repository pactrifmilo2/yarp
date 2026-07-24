using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MyProxy.Admin.Docs;

namespace MyProxy.Tests.Admin;

public class DocsPostmanCollectionTests
{
    [Fact]
    public async Task Postman_collection_endpoint_returns_valid_collection_json()
    {
        using var host = await CreateHostAsync();
        var client = host.GetTestClient();

        var response = await client.GetAsync("/docs/postman");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = document.RootElement;

        Assert.Equal("ATFM Gateway", root.GetProperty("info").GetProperty("name").GetString());
        Assert.True(root.GetProperty("item").GetArrayLength() > 0);
        Assert.Contains(
            root.GetProperty("variable").EnumerateArray(),
            variable => variable.GetProperty("key").GetString() == "gatewayBaseUrl");
    }

    private static Task<IHost> CreateHostAsync()
    {
        var contentRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "MyProxy.Admin"));

        return new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder
                    .UseContentRoot(contentRoot)
                    .UseWebRoot(Path.Combine(contentRoot, "wwwroot"))
                    .UseTestServer()
                    .ConfigureServices(services => services.AddRouting())
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(endpoints => endpoints.MapDocsEndpoints());
                    });
            })
            .StartAsync();
    }
}
