using ClashUp.Tools.FleetController;

var builder = WebApplication.CreateBuilder(args);

// Cloud Run injects the listen port via $PORT (8080 by default).
builder.WebHost.UseUrls($"http://0.0.0.0:{Environment.GetEnvironmentVariable("PORT") ?? "8080"}");

builder.Services.Configure<FleetControllerOptions>(builder.Configuration.GetSection("Fleet"));
builder.Services.AddSingleton<FleetManager>();

var app = builder.Build();

// Liveness only — every other route is gated by Cloud Run IAM (run.invoker), so the
// scheduler SA and the dashboard SA are the only callers that can reach /tick and /wake.
app.MapGet("/healthz", () => Results.Ok("ok"));

app.MapGet("/state", async (FleetManager fleet, CancellationToken ct) =>
    Results.Json(await fleet.GetStateAsync(ct)));

app.MapPost("/tick", async (FleetManager fleet, ILogger<Program> log, CancellationToken ct) =>
{
    var result = await fleet.TickAsync(ct);
    log.LogInformation("Idle tick: {Message}", result.Message);
    return Results.Json(result);
});

app.MapPost("/wake", async (FleetManager fleet, ILogger<Program> log, CancellationToken ct) =>
{
    var result = await fleet.WakeAsync(ct);
    log.LogInformation("Wake: {Message}", result.Message);
    return Results.Json(result);
});

app.Run();
