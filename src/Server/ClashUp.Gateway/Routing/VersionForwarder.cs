using System.Net;
using ClashUp.Server.Gateway.Supervisor;
using Yarp.ReverseProxy.Forwarder;

namespace ClashUp.Server.Gateway.Routing;

/// <summary>
/// Per-request gRPC forwarding: reads <c>x-client-version</c>, asks the
/// supervisor for that version's backend port, and proxies the HTTP/2 call there
/// via YARP's low-level <see cref="IHttpForwarder"/>. Unknown versions get a gRPC
/// <c>FAILED_PRECONDITION</c> + <c>required-action: upgrade-client</c> response —
/// the request never reaches a backend.
/// </summary>
public sealed class VersionForwarder
{
    /// <summary>gRPC status code for FAILED_PRECONDITION.</summary>
    private const string GrpcStatusFailedPrecondition = "9";
    private const string VersionHeader = "x-client-version";

    private readonly IHttpForwarder _forwarder;
    private readonly IProcessSupervisor _supervisor;
    private readonly ILogger<VersionForwarder> _logger;
    private readonly HttpMessageInvoker _httpClient;
    private readonly ForwarderRequestConfig _requestConfig;
    private readonly string _backendHost;
    private readonly string _defaultVersion;

    public VersionForwarder(
        IHttpForwarder forwarder,
        IProcessSupervisor supervisor,
        GatewayOptions options,
        ILogger<VersionForwarder> logger)
    {
        _forwarder = forwarder;
        _supervisor = supervisor;
        _logger = logger;
        _backendHost = options.BackendHost;
        _defaultVersion = options.DefaultVersion;

        _httpClient = new HttpMessageInvoker(new SocketsHttpHandler
        {
            UseProxy = false,
            AllowAutoRedirect = false,
            AutomaticDecompression = System.Net.DecompressionMethods.None,
            UseCookies = false,
            EnableMultipleHttp2Connections = true,
            ConnectTimeout = TimeSpan.FromSeconds(15),
        });

        // Backends are h2c (cleartext HTTP/2) only.
        _requestConfig = new ForwarderRequestConfig
        {
            Version = HttpVersion.Version20,
            VersionPolicy = HttpVersionPolicy.RequestVersionExact,
            ActivityTimeout = TimeSpan.FromSeconds(100),
        };
    }

    public async Task HandleAsync(HttpContext context)
    {
        // Header-less traffic is internal server-to-server — route it to the
        // default tag rather than rejecting it as a stale client.
        var version = ReadVersion(context);
        if (string.IsNullOrEmpty(version))
        {
            version = _defaultVersion;
        }

        int port;
        try
        {
            port = await _supervisor.EnsureVersionAsync(version, context.RequestAborted);
        }
        catch (VersionUnavailableException)
        {
            await WriteUpgradeRequiredAsync(context);
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to bring up backend for version {Version}", version);
            await WriteInternalErrorAsync(context);
            return;
        }

        var destination = $"http://{_backendHost}:{port}";
        var error = await _forwarder.SendAsync(context, destination, _httpClient, _requestConfig);
        if (error != ForwarderError.None && !context.Response.HasStarted)
        {
            var feature = context.Features.Get<IForwarderErrorFeature>();
            _logger.LogWarning(feature?.Exception, "Forwarding to {Destination} failed: {Error}", destination, error);
        }
    }

    private static string ReadVersion(HttpContext context) =>
        context.Request.Headers.TryGetValue(VersionHeader, out var values)
            ? values.ToString().Trim()
            : string.Empty;

    private async Task WriteUpgradeRequiredAsync(HttpContext context)
    {
        // gRPC "trailers-only" response: status carried in HEADERS, no body.
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "application/grpc";

        var headers = context.Response.Headers;
        headers["grpc-status"] = GrpcStatusFailedPrecondition;
        headers["grpc-message"] = "Client version not supported. Please update your game.";
        headers["required-action"] = "upgrade-client";
        headers["server-versions"] = string.Join(",", _supervisor.RunningVersions);

        await context.Response.CompleteAsync();
    }

    private static async Task WriteInternalErrorAsync(HttpContext context)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "application/grpc";
        context.Response.Headers["grpc-status"] = "13"; // INTERNAL
        context.Response.Headers["grpc-message"] = "Gateway could not start a backend for this version.";
        await context.Response.CompleteAsync();
    }
}
