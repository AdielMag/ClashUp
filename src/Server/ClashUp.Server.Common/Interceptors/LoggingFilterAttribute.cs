using System.Threading.Tasks;
using MagicOnion.Server;
using MagicOnion.Server.Filters;
using Microsoft.Extensions.Logging;

namespace ClashUp.Server.Common.Interceptors;

/// <summary>
/// Skeleton MagicOnion filter that times each service call. Production
/// telemetry (OpenTelemetry, structured fields) is added later — see
/// the hardening step in the plan.
/// </summary>
public sealed class LoggingFilterAttribute : MagicOnionFilterAttribute
{
    public override async ValueTask Invoke(ServiceContext context, Func<ServiceContext, ValueTask> next)
    {
        var logger = context.ServiceProvider.GetService(typeof(ILoggerFactory)) as ILoggerFactory;
        var log = logger?.CreateLogger("MagicOnion");

        try
        {
            await next(context);
        }
        catch (System.Exception ex)
        {
            log?.LogError(ex, "MagicOnion call failed: {Method}", context.CallContext.Method);
            throw;
        }
    }
}
