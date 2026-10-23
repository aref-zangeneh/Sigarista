using System.Net;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            if (ex is ValidationException or DomainException or UnauthorizedAccessException or KeyNotFoundException or InvalidOperationException)
            {
                // Expected business/input failures: log a short line instead of a
                // full stack trace, and never include request bodies or tokens.
                _logger.LogWarning("Request failed with {ExceptionType}: {Message}", ex.GetType().Name, ex.Message);
            }
            else
            {
                _logger.LogError(ex, "An unhandled exception occurred for {Method} {Path}.", context.Request.Method, context.Request.Path);
            }

            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, title) = exception switch
        {
            ValidationException => (HttpStatusCode.BadRequest, "Validation failed."),
            DomainException => (HttpStatusCode.BadRequest, "Domain rule violation."),
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "Unauthorized."),
            KeyNotFoundException => (HttpStatusCode.NotFound, "Resource not found."),
            InvalidOperationException => (HttpStatusCode.BadRequest, "Request could not be completed."),
            _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred.")
        };

        var problemDetails = new ProblemDetails
        {
            Status = (int)statusCode,
            Title = title,
            Detail = statusCode == HttpStatusCode.InternalServerError
                ? "خطای غیرمنتظره‌ای رخ داد. لطفاً دوباره تلاش کنید."
                : exception.Message,
            Instance = context.Request.Path
        };

        problemDetails.Extensions["traceId"] = context.TraceIdentifier;

        if (exception is ValidationException validationException)
        {
            problemDetails.Extensions["errors"] = validationException.Errors
                .GroupBy(error => error.PropertyName)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(error => error.ErrorMessage).ToArray());
        }

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = (int)statusCode;

        return context.Response.WriteAsJsonAsync(problemDetails);
    }
}

public static class ExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseExceptionHandling(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ExceptionHandlingMiddleware>();
    }
}
