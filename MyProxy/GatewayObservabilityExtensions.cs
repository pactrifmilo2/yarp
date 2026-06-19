using MyProxy.Infrastructure.Metrics;
using OpenTelemetry.Metrics;

namespace MyProxy;

internal static class GatewayObservabilityExtensions
{
    public static IServiceCollection AddGatewayObservability(this IServiceCollection services)
    {
        services.AddOpenTelemetry()
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddMeter(GatewayMetrics.MeterName)
                .AddPrometheusExporter());

        return services;
    }
}
