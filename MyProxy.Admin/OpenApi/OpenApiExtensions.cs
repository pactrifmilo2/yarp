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
                    Title = "MyProxy Control Plane API",
                    Version = "v1",
                    Description = "Manage API clients, routes, scopes, rate limits, and audit logs for the YARP gateway.",
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
            options.SwaggerEndpoint("/openapi/v1.json", "MyProxy Control Plane API");
        });

        return app;
    }
}
