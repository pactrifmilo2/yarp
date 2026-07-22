namespace MyProxy.Infrastructure.Auth;

public sealed class ApiKeyBypassOptions
{
    public const string SectionName = "Authentication:ApiKeyBypass";

    public bool Enabled { get; set; }

    public string[] AllowedIpAddresses { get; set; } = [];
}
