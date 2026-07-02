using ClashUp.Tools.FleetController;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Cloud Run injects the listen port via $PORT (8080 by default).
builder.WebHost.UseUrls($"http://0.0.0.0:{Environment.GetEnvironmentVariable("PORT") ?? "8080"}");

builder.Services.Configure<FleetControllerOptions>(builder.Configuration.GetSection("Fleet"));
builder.Services.AddSingleton<AtlasAccessListClient>();
builder.Services.AddSingleton<FleetManager>();

var app = builder.Build();

var opts = app.Services.GetRequiredService<IOptions<FleetControllerOptions>>().Value;

// The client discovers the Services IP here at boot, so this service must accept
// public ingress (allUsers run.invoker) — it can't present an OIDC token before it
// even has an endpoint. Cloud Run IAM is therefore open, so we gate routes in-app
// with shared keys instead: a low-privilege ResolveKey for the public /resolve wake
// path, and an AdminKey for the dashboard's /wake + /state controls.
//
// /tick is deliberately UNAUTHENTICATED: it's the Cloud Scheduler idle check, and
// the scheduler authenticates with an OIDC token, NOT the X-ClashUp-Key header — so
// gating it with a key permanently 401s the scheduler and the fleet never sleeps.
// Leaving it open is safe: /tick only ever SLEEPS a genuinely-idle fleet (CCU 0
// across the lookback window); it can't force-sleep an active fleet or wake anything.
static bool KeyMatches(HttpContext ctx, string expected) =>
    !string.IsNullOrEmpty(expected) &&
    ctx.Request.Headers.TryGetValue("X-ClashUp-Key", out var got) &&
    string.Equals(got.ToString(), expected, StringComparison.Ordinal);

// Liveness only — always open.
app.MapGet("/healthz", () => Results.Ok("ok"));

// Public boot discovery: returns the live Services endpoint, waking the fleet if asleep.
app.MapGet("/resolve", async (FleetManager fleet, HttpContext ctx, ILogger<Program> log, CancellationToken ct) =>
{
    if (!KeyMatches(ctx, opts.ResolveKey))
    {
        return Results.Unauthorized();
    }

    var endpoint = await fleet.ResolveServicesEndpointAsync(ct);
    log.LogInformation("Resolve -> {Endpoint}", endpoint);
    return Results.Json(new { endpoint });
});

app.MapGet("/state", async (FleetManager fleet, HttpContext ctx, CancellationToken ct) =>
    KeyMatches(ctx, opts.AdminKey)
        ? Results.Json(await fleet.GetStateAsync(ct))
        : Results.Unauthorized());

// Unauthenticated by design — see the note on KeyMatches above. The scheduler's
// OIDC token can't carry the X-ClashUp-Key header, and /tick only sleeps an idle fleet.
app.MapPost("/tick", async (FleetManager fleet, ILogger<Program> log, CancellationToken ct) =>
{
    var result = await fleet.TickAsync(ct);
    log.LogInformation("Idle tick: {Message}", result.Message);
    return Results.Json(result);
});

app.MapPost("/wake", async (FleetManager fleet, HttpContext ctx, ILogger<Program> log, CancellationToken ct) =>
{
    if (!KeyMatches(ctx, opts.AdminKey))
    {
        return Results.Unauthorized();
    }

    var result = await fleet.WakeAsync(ct);
    log.LogInformation("Wake: {Message}", result.Message);
    return Results.Json(result);
});

app.Run();
