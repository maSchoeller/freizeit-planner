using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace FreizeitCockpit.ServiceDefaults;

public static class FreizeitCorrelation
{
    public const string HeaderName = "X-Correlation-ID";
    public const string ActivitySourceName = "FreizeitCockpit.Operations";

    private static readonly ActivitySource Operations = new(ActivitySourceName);

    public static string Resolve(string? candidate)
    {
        if (IsValidTraceId(candidate))
        {
            return candidate!;
        }

        var current = Activity.Current?.TraceId.ToString();
        return IsValidTraceId(current)
            ? current!
            : ActivityTraceId.CreateRandom().ToString();
    }

    public static IDisposable BeginOperation(ILogger logger, string operationName)
    {
        var activity = Operations.StartActivity(operationName, ActivityKind.Internal)
            ?? new Activity(operationName).Start();
        var correlationId = Resolve(activity.TraceId.ToString());
        var loggingScope = logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["Operation"] = operationName
        });
        return new OperationScope(activity, loggingScope);
    }

    private static bool IsValidTraceId(string? value)
    {
        if (value is not { Length: 32 } || value.All(character => character == '0'))
        {
            return false;
        }

        return value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');
    }

    private sealed class OperationScope(Activity activity, IDisposable? loggingScope) : IDisposable
    {
        public void Dispose()
        {
            loggingScope?.Dispose();
            activity.Dispose();
        }
    }
}

public sealed class CorrelationIdMiddleware(
    RequestDelegate next,
    ILogger<CorrelationIdMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var candidate = context.Request.Headers[FreizeitCorrelation.HeaderName].FirstOrDefault();
        var correlationId = FreizeitCorrelation.Resolve(candidate);
        context.TraceIdentifier = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[FreizeitCorrelation.HeaderName] = correlationId;
            return Task.CompletedTask;
        });
        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        });
        await next(context);
    }
}
