using ClashUp.Server.Gateway;
using ClashUp.Server.Gateway.Routing;
using ClashUp.Server.Gateway.Supervisor;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Yarp.ReverseProxy.Forwarder;

namespace ClashUp.Server.GameServer.Tests.Gateway;

public class VersionForwarderTests
{
    private sealed class FakeForwarder : IHttpForwarder
    {
        public string? LastDestination;

        public ValueTask<ForwarderError> SendAsync(HttpContext context, string destinationPrefix,
            HttpMessageInvoker httpClient, ForwarderRequestConfig requestConfig, HttpTransformer transformer)
        {
            LastDestination = destinationPrefix;
            return new ValueTask<ForwarderError>(ForwarderError.None);
        }
    }

    private sealed class FakeSupervisor : IProcessSupervisor
    {
        public Func<string, int> OnEnsure = _ => 0;
        public IReadOnlyCollection<string> RunningVersions { get; set; } = new[] { "1.0.0", "1.1.0" };

        public Task<int> EnsureVersionAsync(string version, CancellationToken ct) => Task.FromResult(OnEnsure(version));
        public IReadOnlyList<BackendStatus> GetStatus() => Array.Empty<BackendStatus>();
        public Task RunMaintenanceAsync(CancellationToken ct) => Task.CompletedTask;
        public Task PrewarmAsync(CancellationToken ct) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static HttpContext Context(string? version)
    {
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();
        if (version != null)
            ctx.Request.Headers["x-client-version"] = version;
        return ctx;
    }

    private static VersionForwarder Build(FakeForwarder fwd, FakeSupervisor sup) =>
        new(fwd, sup, new GatewayOptions { BackendHost = "127.0.0.1", DefaultVersion = "latest" },
            NullLogger<VersionForwarder>.Instance);

    [Fact]
    public async Task UnknownVersion_WritesGrpcUpgradeRequired()
    {
        var sup = new FakeSupervisor { OnEnsure = v => throw new VersionUnavailableException(v) };
        var forwarder = new FakeForwarder();
        var sut = Build(forwarder, sup);
        var ctx = Context("9.9.9");

        await sut.HandleAsync(ctx);

        ctx.Response.Headers["grpc-status"].ToString().Should().Be("9", "FAILED_PRECONDITION");
        ctx.Response.Headers["required-action"].ToString().Should().Be("upgrade-client");
        ctx.Response.Headers["server-versions"].ToString().Should().Contain("1.0.0");
        forwarder.LastDestination.Should().BeNull("the request never reaches a backend");
    }

    [Fact]
    public async Task KnownVersion_ForwardsToBackendPort()
    {
        var sup = new FakeSupervisor { OnEnsure = _ => 1234 };
        var forwarder = new FakeForwarder();
        var sut = Build(forwarder, sup);

        await sut.HandleAsync(Context("1.0.0"));

        forwarder.LastDestination.Should().Be("http://127.0.0.1:1234");
    }

    [Fact]
    public async Task MissingVersionHeader_UsesDefaultVersion()
    {
        string? seen = null;
        var sup = new FakeSupervisor { OnEnsure = v => { seen = v; return 5101; } };
        var sut = Build(new FakeForwarder(), sup);

        await sut.HandleAsync(Context(version: null));

        seen.Should().Be("latest", "header-less internal traffic routes to the default tag");
    }
}
