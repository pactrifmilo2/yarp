namespace MyProxy.Admin.Docs;

public static class DocsEndpoints
{
    public const string PostmanCollectionFileName = "myproxy-gateway.postman_collection.json";

    public static IEndpointRouteBuilder MapDocsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/docs/postman", (IWebHostEnvironment environment) =>
        {
            var path = Path.Combine(environment.WebRootPath, "docs", PostmanCollectionFileName);

            if (!File.Exists(path))
            {
                return Results.NotFound();
            }

            return Results.File(
                path,
                contentType: "application/json",
                fileDownloadName: PostmanCollectionFileName);
        });

        return endpoints;
    }
}
