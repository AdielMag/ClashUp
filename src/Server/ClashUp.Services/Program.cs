using ClashUp.Server.Common.Auth;
using ClashUp.Server.Common.Configuration;
using ClashUp.Server.Common.Interceptors;
using ClashUp.Server.Common.Mongo;
using ClashUp.Server.Services.Matchmaking;
using ClashUp.Server.Services.Persistence;
using ClashUp.Shared.Services;
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
    .AddOptions<MongoOptions>()
    .Bind(builder.Configuration.GetSection(MongoOptions.SectionName));

builder.Services
    .AddOptions<MatchmakingOptions>()
    .Bind(builder.Configuration.GetSection(MatchmakingOptions.SectionName));

builder.Services.AddSingleton<IJwtKeyProvider, JwtKeyProvider>();
builder.Services.AddSingleton<IJwtTokenIssuer, JwtTokenIssuer>();
builder.Services.AddSingleton<IMongoContext, MongoContext>();

builder.Services.AddSingleton<IAccountRepository, AccountRepository>();
builder.Services.AddSingleton<IMatchRepository, MatchRepository>();
builder.Services.AddSingleton<IGameServerInstanceRepository, GameServerInstanceRepository>();

builder.Services.AddSingleton<IIndexInitializer, AccountIndexInitializer>();
builder.Services.AddSingleton<IIndexInitializer, MatchIndexInitializer>();
builder.Services.AddSingleton<IIndexInitializer, GameServerInstanceIndexInitializer>();
builder.Services.AddHostedService<IndexBootstrapper>();

builder.Services.AddSingleton<MatchmakingQueue>();
builder.Services.AddSingleton<GameServerAdminClientFactory>();
builder.Services.AddSingleton<IGameServerProvisioner, GameServerProvisionerStub>();
builder.Services.AddHostedService<Matchmaker>();

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
