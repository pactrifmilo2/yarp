using Microsoft.Extensions.Logging;

namespace MyProxy.Admin.ControlPlane;

public sealed class GatewayReloadClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GatewayReloadClient> _logger;

    public GatewayReloadClient(HttpClient httpClient, ILogger<GatewayReloadClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsync("/api/reload", null, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Gateway config reloaded successfully.");
            }
            else
            {
                _logger.LogWarning(
                    "Gateway reload returned {StatusCode} {Reason}.",
                    (int)response.StatusCode,
                    response.ReasonPhrase);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(
                exception,
                "Failed to notify Gateway to reload config. The Gateway may not be running or reachable.");
        }
    }
}
