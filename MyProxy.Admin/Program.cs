using MyProxy.Admin.Components;
using MyProxy.Admin.ControlPlane;
using MyProxy.Admin.Docs;
using MyProxy.Admin.OpenApi;
using MyProxy.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
var useHttpsRedirection = builder.Configuration.GetValue<bool>("Hosting:UseHttpsRedirection");

builder.Services.AddGatewayInfrastructure(builder.Configuration);
builder.Services.AddGatewayOpenApi();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpClient<GatewayReloadClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Gateway:BaseUrl"]!);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    if (useHttpsRedirection)
    {
        app.UseHsts();
    }
}

if (useHttpsRedirection)
{
    app.UseHttpsRedirection();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAntiforgery();

app.MapStaticAssets();
app.MapGatewayOpenApi();
app.MapDocsEndpoints();
app.MapGet("/health", () => TypedResults.Ok(new
{
    service = "ATFM Gateway Admin",
    status = "healthy",
}));

var gatewayReloadClient = app.Services.GetRequiredService<GatewayReloadClient>();
app.MapControlPlaneApi(gatewayReloadClient);

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
