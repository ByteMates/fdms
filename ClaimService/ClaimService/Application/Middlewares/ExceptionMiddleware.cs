// ClaimService.Application/Middlewares/ExceptionMiddleware.cs
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;       // <-- needed for IMiddleware, HttpContext, RequestDelegate
using Microsoft.AspNetCore.Mvc;
using ClaimService.Application.Common.Errors;

namespace ClaimService.Application.Middlewares;

public sealed class ExceptionMiddleware : IMiddleware
{
    private readonly ILogger<ExceptionMiddleware> _logger;
    public ExceptionMiddleware(ILogger<ExceptionMiddleware> logger) => _logger = logger;

    public async Task InvokeAsync(HttpContext ctx, RequestDelegate next)
    {
        try
        {
            await next(ctx);
        }
        catch (OperationCanceledException) when (ctx.RequestAborted.IsCancellationRequested)
        {
            ctx.Response.StatusCode = 499;
            await ctx.Response.CompleteAsync();
        }
        catch (ValidationException vex)
        {
            await WriteProblem(ctx, (int)HttpStatusCode.BadRequest, vex.Message, vex.ErrorCode, errors: vex.Errors);
        }
        catch (NotFoundException nfex)
        {
            await WriteProblem(ctx, (int)HttpStatusCode.NotFound, nfex.Message, nfex.ErrorCode);
        }
        catch (AppException aex)
        {
            await WriteProblem(ctx, (int)aex.StatusCode, aex.Message, aex.ErrorCode);
        }
        catch (HttpRequestException httpEx)
        {
            var status = httpEx.StatusCode.HasValue ? (int)httpEx.StatusCode.Value : StatusCodes.Status502BadGateway;
            _logger.LogWarning(httpEx, "Upstream call failed");
            await WriteProblem(ctx, status, "Upstream service error", "UpstreamError", detail: httpEx.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            await WriteProblem(ctx, StatusCodes.Status500InternalServerError, "An unexpected error occurred.", "ServerError");
        }
    }

    private static async Task WriteProblem(HttpContext ctx, int status, string title, string type,
                                          string? detail = null, IDictionary<string, string[]>? errors = null)
    {
        ctx.Response.ContentType = "application/problem+json";
        ctx.Response.StatusCode = status;

        var pd = new ProblemDetails
        {
            Status = status,
            Title = title,
            Type = type,
            Detail = detail,
            Instance = ctx.Request.Path
        };
        pd.Extensions["traceId"] = ctx.TraceIdentifier;
        if (errors is not null && errors.Count > 0) pd.Extensions["errors"] = errors;

        var json = JsonSerializer.Serialize(pd, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        await ctx.Response.WriteAsync(json);
    }
}
