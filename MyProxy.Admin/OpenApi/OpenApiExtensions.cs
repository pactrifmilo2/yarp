using Microsoft.AspNetCore.OpenApi;

namespace MyProxy.Admin.OpenApi;

public static class OpenApiExtensions
{
    public static IServiceCollection AddGatewayOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, context, cancellationToken) =>
            {
                document.Info = new()
                {
                    Title = "ATFM Gateway Admin API",
                    Version = "v1",
                    Description = "Manage API clients, routes, scopes, rate limits, and audit logs for ATFM Gateway.",
                };
                return Task.CompletedTask;
            });
        });

        return services;
    }

    public static WebApplication MapGatewayOpenApi(this WebApplication app)
    {
        app.MapOpenApi();

        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/openapi/v1.json", "ATFM Gateway Admin API");
        });

        return app;
    }
}
