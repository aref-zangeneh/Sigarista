using Microsoft.Extensions.Options;

namespace Sugarista.Api.Auth;

public class PrintAgentOptions
{
    public const string SectionName = "PrintAgent";

    /// <summary>Shared secret the print agent sends in the <see cref="HeaderName"/> header.</summary>
    public string ApiKey { get; set; } = string.Empty;
}

/// <summary>
/// Shared-key gate for the receipt print agent endpoints
/// (<c>/api/print-agent/*</c>). The agent runs on the store laptop next to
/// the receipt printer, so a simple static bearer-style key is enough —
/// the endpoints are minimal and read-only apart from acknowledging jobs.
/// </summary>
public class PrintAgentKeyMiddleware
{
    public const string HeaderName = "X-Agent-Key";

    private readonly RequestDelegate _next;
    private readonly string? _apiKey;

    public PrintAgentKeyMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _apiKey = configuration.GetSection(PrintAgentOptions.SectionName)[nameof(PrintAgentOptions.ApiKey)];
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api/print-agent"))
        {
            await _next(context);
            return;
        }

        var provided = context.Request.Headers[HeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(_apiKey) || string.IsNullOrWhiteSpace(provided) || provided != _apiKey)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new
            {
                title = "Print agent key required.",
                status = 401
            });
            return;
        }

        await _next(context);
    }
}
