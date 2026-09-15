using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Norn.BuildingBlocks.Web.Errors;

/// <summary>Erros seguem RFC 9457 (Problem Details) — §5.2.</summary>
internal sealed partial class NornExceptionHandler(ILogger<NornExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        LogUnhandledException(logger, exception);
        AppErrorMetrics.RecordException(exception);

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Erro interno não tratado.",
            Detail = exception.Message,
            Type = "https://tools.ietf.org/html/rfc9457",
            Instance = httpContext.Request.Path,
        };
        problemDetails.Extensions["traceId"] = System.Diagnostics.Activity.Current?.Id ?? httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = problemDetails.Status.Value;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Exceção não tratada.")]
    private static partial void LogUnhandledException(ILogger logger, Exception exception);
}
