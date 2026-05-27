using ClashUp.Server.Common.Auth;
using ClashUp.Server.Common.Configuration;
using ClashUp.Server.Common.Interceptors;
using ClashUp.Server.GameServer.Match;
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
