using ClashUp.Tools.Dashboard;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<DashboardOptions>(builder.Configuration.GetSection("Gcp"));
builder.Services.AddSingleton<GcpStatusService>();

// Export the configured key path as ADC so the Google client libraries pick it
// up. If unset, ambient ADC (GOOGLE_APPLICATION_CREDENTIALS / gcloud login) wins.
var credentialsPath = builder.Configuration["Gcp:CredentialsPath"];
if (!string.IsNullOrWhiteSpace(credentialsPath))
{
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

app.Run();

internal sealed record DeleteVersionRequest(string Image, string Tag);
