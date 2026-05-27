using ClashUp.Server.Common.Auth;
using ClashUp.Server.Common.Configuration;
using ClashUp.Server.Common.Interceptors;
using ClashUp.Server.GameServer.Match;
using ClashUp.Server.GameServer.Registration;
using ClashUp.Server.GameServer.Simulation;
using MagicOnion.Server;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, _, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services
    .AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName));

builder.Services
    .AddOptions<GameServerOptions>()
    .Bind(builder.Configuration.GetSection(GameServerOptions.SectionName));

builder.Services.AddSingleton<IJwtKeyProvider, JwtKeyProvider>();
builder.Services.AddSingleton<IMatchRegistry, MatchRegistry>();

// Per-match scoped simulation pieces. Resolved via MatchContext's
// IServiceScope. Swap NullServerSimulation for the AetherNet adapter
// once external/AetherNet/ is on disk.
builder.Services.AddScoped<IServerSimulation, NullServerSimulation>();
builder.Services.AddScoped<InputBuffer>();
builder.Services.AddScoped<MatchClock>();

builder.Services.AddSingleton<GameServerIdentity>();
builder.Services.AddSingleton<IServicesRegistryClient, ServicesRegistryClient>();
builder.Services.AddHostedService<GameServerRegistrar>();
builder.Services.AddHostedService<HeartbeatBackgroundService>();

builder.Services.AddGrpc();
builder.Services.AddMagicOnion(options =>
{
    options.GlobalFilters.Add<LoggingFilterAttribute>();
});

var app = builder.Build();

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

app.MapMagicOnionService();

app.Run();

public partial class Program { }
