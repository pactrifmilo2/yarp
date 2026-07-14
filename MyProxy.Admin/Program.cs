using MyProxy.Admin.Components;
using MyProxy.Admin.ControlPlane;
using MyProxy.Admin.Docs;
using MyProxy.Admin.OpenApi;
using MyProxy.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

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
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapGatewayOpenApi();
app.MapDocsEndpoints();

var gatewayReloadClient = app.Services.GetRequiredService<GatewayReloadClient>();
app.MapControlPlaneApi(gatewayReloadClient);

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
