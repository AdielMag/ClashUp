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
