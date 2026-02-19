using Microsoft.Extensions.Logging;

namespace Worker.Observability;

public static class LogScopes
{
    public static IDisposable BeginCycleScope(this ILogger logger, string cycleId)
        => logger.BeginScope(new Dictionary<string, object>
        {
            ["cycleId"] = cycleId
        });
}