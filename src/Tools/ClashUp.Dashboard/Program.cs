using ClashUp.Tools.Dashboard;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<DashboardOptions>(builder.Configuration.GetSection("Gcp"));
builder.Services.AddSingleton<GcpStatusService>();

// Export the configured key path as ADC so the Google client libraries pick it
// up. A relative path is resolved against the content root so the dashboard works
// no matter the launch directory (e.g. `dotnet run --project ...` from repo root).
// If the resolved file is missing, fall back to ambient ADC (GOOGLE_APPLICATION_
// CREDENTIALS / `gcloud auth application-default login`).
var credentialsPath = builder.Configuration["Gcp:CredentialsPath"];
if (!string.IsNullOrWhiteSpace(credentialsPath))
{
    if (!Path.IsPathRooted(credentialsPath))
        credentialsPath = Path.Combine(builder.Environment.ContentRootPath, credentialsPath);

    if (File.Exists(credentialsPath))
        Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", credentialsPath);
}

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/status", async (GcpStatusService svc, CancellationToken ct) =>
    Results.Json(await svc.GetFleetStatusAsync(ct)));

// Uptime calendar: per-day awake-hours over a date range (defaults to the ~6-week
// window Cloud Monitoring retains the source metric for). `from`/`to` are UTC dates
// (yyyy-MM-dd); omit either to use the default rolling window.
app.MapGet("/api/uptime", async (GcpStatusService svc, DateOnly? from, DateOnly? to, CancellationToken ct) =>
{
    var toDate = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
    var fromDate = from ?? toDate.AddDays(-41);
    try
    {
        return Results.Json(await svc.GetUptimeCalendarAsync(fromDate, toDate, ct));
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/api/registry/delete", async (GcpStatusService svc, DeleteVersionRequest req, CancellationToken ct) =>
{
    try
    {
        await svc.DeleteImageVersionAsync(req.Image, req.Tag, ct);
        return Results.Ok(new { ok = true });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/api/wake", async (GcpStatusService svc, CancellationToken ct) =>
{
    try
    {
        await svc.WakeFleetAsync(ct);
        return Results.Ok(new { ok = true });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.Run();

internal sealed record DeleteVersionRequest(string Image, string Tag);
