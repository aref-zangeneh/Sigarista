using Sugarista.Infrastructure.Data.DbConnection;
using Sugarista.Infrastructure.Settings;

namespace Sugarista.Api.Middleware;

/// <summary>
/// Lets the admin open/close special public paths at runtime from the admin
/// panel. Currently controllable:
///   • /api/setup (+ /setup.html) — closed by default once the database is
///     configured; re-openable from the admin panel for e.g. password rotation.
///   • /health — open by default; closing it only affects requests that came
///     through the reverse proxy (X-Forwarded-For), never the container's
///     own healthcheck, so a "closed" health endpoint can not mark the
///     container unhealthy or take the stack down.
/// Flags live in app-setup.json and survive rebuilds/restarts.
/// </summary>
public sealed class RouteControlMiddleware
{
    private const string ForwardedForHeader = "X-Forwarded-For";
    private const string PublicMarkerHeader = "X-Sugarista-Public";

    private readonly RequestDelegate _next;
    private readonly DbConnectionManager _connectionManager;
    private readonly AppSetupStore _appSetupStore;
    private readonly ILogger<RouteControlMiddleware> _logger;

    public RouteControlMiddleware(
        RequestDelegate next,
        DbConnectionManager connectionManager,
        AppSetupStore appSetupStore,
        ILogger<RouteControlMiddleware> logger)
    {
        _next = next;
        _connectionManager = connectionManager;
        _appSetupStore = appSetupStore;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (IsBlocked(context))
        {
            _logger.LogWarning(
                "Blocked request to {Path} from {RemoteIp} — the route is disabled in the admin panel.",
                context.Request.Path,
                context.Connection.RemoteIpAddress);

            context.Response.StatusCode = StatusCodes.Status404NotFound;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                "{\"success\":false,\"message\":\"این مسیر از پنل مدیریت بسته شده است.\"}");
            return;
        }

        await _next(context);
    }

    private bool IsBlocked(HttpContext context)
    {
        if (!_connectionManager.IsConfigured)
        {
            // First-run mode: the setup gate owns everything; the setup page
            // must stay reachable so a never-configured install can not be
            // locked out of its own configuration endpoint.
            return false;
        }

        var path = context.Request.Path;
        var secrets = _appSetupStore.Load();

        // /api/setup + /setup.html — default CLOSED once configured.
        if (IsSetupPath(path) && !(secrets.SetupPageEnabled ?? false))
        {
            return true;
        }

        // /health — default OPEN, controlled from the admin panel. The nginx
        // proxy marks requests that came from outside with X-Sugarista-Public,
        // while the container's own healthcheck calls /health without it — so
        // closing /health never marks the container unhealthy.
        if (IsHealthPath(path) && !(secrets.PublicHealthEnabled ?? true))
        {
            return context.Request.Headers.ContainsKey(PublicMarkerHeader) ||
                   context.Request.Headers.ContainsKey(ForwardedForHeader);
        }

        return false;
    }

    private static bool IsSetupPath(PathString path)
    {
        if (path.StartsWithSegments("/api/setup"))
        {
            return true;
        }

        return path.Value is not null &&
               (path.Value.Equals("/setup.html", StringComparison.OrdinalIgnoreCase) ||
                path.Value.StartsWith("/setup.html?", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsHealthPath(PathString path) =>
        path.Value is not null &&
        (path.Value.Equals("/health", StringComparison.OrdinalIgnoreCase) ||
         path.Value.StartsWith("/health/", StringComparison.OrdinalIgnoreCase));
}
