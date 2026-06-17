using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ClashUp.Client.Networking
{
    /// <summary>
    /// Adds the <c>x-client-version</c> header (the app's version) to every gRPC
    /// request on the channel it wraps — unary calls AND StreamingHub connects —
    /// so the server-side version gateway can route to the matching backend.
    /// gRPC metadata is carried as HTTP/2 headers, so doing this at the
    /// HttpMessageHandler layer covers all call types in one place.
    /// </summary>
    public sealed class ClientVersionHttpHandler : DelegatingHandler
    {
        public const string HeaderName = "x-client-version";

        private readonly string _version;

        public ClientVersionHttpHandler(HttpMessageHandler inner, string version)
        {
            InnerHandler = inner;
            _version = version;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Headers.Remove(HeaderName);
            request.Headers.TryAddWithoutValidation(HeaderName, _version);
            return base.SendAsync(request, cancellationToken);
        }
    }
}
