using ClashUp.Server.Gateway;
using ClashUp.Server.Gateway.Routing;
using ClashUp.Server.Gateway.Supervisor;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, _, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services
    .AddOptions<GatewayOptions>()
    .Bind(builder.Configuration.GetSection(GatewayOptions.SectionName));

var gatewayOptions = builder.Configuration
    .GetSection(GatewayOptions.SectionName)
    .Get<GatewayOptions>() ?? new GatewayOptions();

// Two endpoints: HTTP/2 (h2c) for gRPC forwarding, plain HTTP/1 for health/admin.
builder.WebHost.ConfigureKestrel(kestrel =>
{
    kestrel.ListenAnyIP(gatewayOptions.ListenPort, listen => listen.Protocols = HttpProtocols.Http2);
    kestrel.ListenAnyIP(gatewayOptions.AdminPort, listen => listen.Protocols = HttpProtocols.Http1);
});

builder.Services.AddSingleton(_ => gatewayOptions);
builder.Services.AddSingleton<IProcessSupervisor, ProcessSupervisor>();
builder.Services.AddSingleton<VersionForwarder>();
builder.Services.AddHostedService<SupervisorHostedService>();
builder.Services.AddHttpForwarder();

var app = builder.Build();

// Health + diagnostics on the admin (HTTP/1) port.
app.MapGet("/healthz", () => Results.Ok(new { status = "ok", tier = gatewayOptions.Tier }))
    .RequireHost($"*:{gatewayOptions.AdminPort}");

app.MapGet("/admin/status", (IProcessSupervisor supervisor) => Results.Json(new
{
    tier = gatewayOptions.Tier,
    version = ClashUp.Server.Common.ServerVersion.Current,
    listenPort = gatewayOptions.ListenPort,
    backends = supervisor.GetStatus(),
})).RequireHost($"*:{gatewayOptions.AdminPort}");

// Everything else on the gRPC port is version-routed to a backend.
app.Map("/{**catchAll}", (HttpContext context, VersionForwarder forwarder) => forwarder.HandleAsync(context))
    .RequireHost($"*:{gatewayOptions.ListenPort}");

app.Run();

public partial class Program { }
