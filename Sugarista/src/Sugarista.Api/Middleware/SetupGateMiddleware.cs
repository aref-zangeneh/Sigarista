using System.Text.Json;
using Sugarista.Infrastructure.Data.DbConnection;

namespace Sugarista.Api.Middleware;

/// <summary>
/// Blocks every request while the database is not configured yet (first-run
/// "setup mode", like a fresh WordPress install). Only the setup endpoints and
/// health checks are allowed through; everything else gets a descriptive 503.
/// </summary>
public sealed class SetupGateMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(System.Text.Json.JsonSerializerDefaults.Web);

    private readonly RequestDelegate _next;
    private readonly DbConnectionManager _manager;

    public SetupGateMiddleware(RequestDelegate next, DbConnectionManager manager)
    {
        _next = next;
        _manager = manager;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (_manager.IsConfigured || IsAllowed(context.Request.Path))
        {
            await _next(context);
            return;
        }

        if (!context.Response.HasStarted)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.ContentType = "application/json; charset=utf-8";

            await context.Response.WriteAsync(
                JsonSerializer.Serialize(
                    new
                    {
                        setupRequired = true,
                        message = "دیتابیس هنوز تنظیم نشده است. صفحهٔ تنظیم را در /api/setup باز کنید.",
                        setupUrl = "/api/setup"
                    },
                    JsonOptions),
                context.RequestAborted);
        }
    }

    private static bool IsAllowed(PathString path) =>
        path.StartsWithSegments("/api/setup") ||
        path == "/health" ||
        path.StartsWithSegments("/api/files") ||
        path == "/favicon.ico";
}
