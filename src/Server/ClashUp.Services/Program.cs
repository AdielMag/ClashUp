using ClashUp.Server.Common.Auth;
using ClashUp.Server.Common.Configuration;
using ClashUp.Server.Common.Interceptors;
using ClashUp.Server.Common.Mongo;
using ClashUp.Server.Services.Persistence;
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

builder.Services.AddSingleton<IJwtKeyProvider, JwtKeyProvider>();
builder.Services.AddSingleton<IMongoContext, MongoContext>();

builder.Services.AddSingleton<IGameServerInstanceRepository, GameServerInstanceRepository>();
builder.Services.AddSingleton<IIndexInitializer, GameServerInstanceIndexInitializer>();
builder.Services.AddHostedService<IndexBootstrapper>();

builder.Services.AddGrpc();
builder.Services.AddMagicOnion(options =>
{
    options.GlobalFilters.Add<LoggingFilterAttribute>();
});

var app = builder.Build();

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

app.MapMagicOnionService();

app.Run();

// Make Program partial so integration tests (added later) can reference it.
public partial class Program { }
