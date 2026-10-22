using System.Security.Claims;
using System.Text;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Sugarista.Application.ActivityLogs;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Api.Filters;

/// <summary>
/// Global resource filter that turns every mutating admin request (plus login /
/// logout / exports) into one human-readable activity-log row. It never breaks
/// the request: any logging failure is swallowed and only written to the log sink.
/// </summary>
public sealed class ActivityLogFilter : IAsyncResourceFilter
{
    private static readonly HashSet<string> MutationMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "POST", "PUT", "PATCH", "DELETE",
    };

    private static readonly string[] LabelCandidateKeys =
    [
        "persianName", "englishName", "fullName", "displayName", "customerName", "name",
        "title", "trackingCode", "code", "serialNumber", "username", "tableNumber",
    ];

    private const int MaxPathLength = 300;
    private const int MaxUserAgentLength = 300;
    private const int MaxEntityLabelLength = 120;

    private readonly ICurrentUserService _currentUser;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ActivityLogFilter> _logger;

    public ActivityLogFilter(
        ICurrentUserService currentUser,
        IServiceScopeFactory scopeFactory,
        ILogger<ActivityLogFilter> logger)
    {
        _currentUser = currentUser;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var http = context.HttpContext;
        var path = http.Request.Path.Value ?? string.Empty;

        if (!ShouldLog(http.Request.Method, path))
        {
            await next();
            return;
        }

        var body = await ReadBodyAsync(http.Request);
        Exception? failure = null;

        try
        {
            var executed = await next();

            if (executed.Exception is not null && !executed.ExceptionHandled)
            {
                failure = executed.Exception;
            }
        }
        catch (Exception exception)
        {
            failure = exception;
            throw;
        }
        finally
        {
            await TryWriteAsync(http, path, body, failure);
        }
    }

    private static bool ShouldLog(string method, string path)
    {
        var isAuthEndpoint = path.StartsWith("/api/auth/", StringComparison.OrdinalIgnoreCase);

        if (!isAuthEndpoint &&
            !path.StartsWith("/api/admin", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // reading the log must not pollute the timeline (purges are still tracked)
        if (path.StartsWith("/api/admin/activity-logs", StringComparison.OrdinalIgnoreCase) &&
            !path.EndsWith("/purge", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (path.StartsWith("/api/files", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (MutationMethods.Contains(method))
        {
            return true;
        }

        // downloads of report exports are GETs but still worth tracking
        return string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase) &&
               path.Contains("/exports", StringComparison.OrdinalIgnoreCase) &&
               path.EndsWith("/download", StringComparison.OrdinalIgnoreCase);
    }

    private async Task TryWriteAsync(
        HttpContext http,
        string path,
        RequestBodyInfo body,
        Exception? failure)
    {
        try
        {
            // the exception middleware runs after this filter, so map the failure
            // to the same status code the client is about to receive
            var statusCode = failure switch
            {
                ValidationException => StatusCodes.Status400BadRequest,
                DomainException => StatusCodes.Status400BadRequest,
                InvalidOperationException => StatusCodes.Status400BadRequest,
                UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
                KeyNotFoundException => StatusCodes.Status404NotFound,
                not null => StatusCodes.Status500InternalServerError,
                _ => http.Response.StatusCode,
            };

            var succeeded = failure is null && statusCode < 400;
            var (sectionId, sectionTitle) = AdminSections.Resolve(path);
            var action = ResolveAction(http.Request.Method, path, body);
            var changedFields = succeeded &&
                                action is ActivityActions.Update or ActivityActions.Status or ActivityActions.Create
                ? body.FieldLabels
                : Array.Empty<string>();

            var actorUsername = _currentUser.IsAuthenticated
                ? http.User.Identity?.Name ?? "unknown"
                : body.Username ?? "ناشناس";

            var actorDisplayName = http.User.FindFirstValue("display_name");

            await using var scope = _scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

            Guid? actorId = Guid.TryParse(_currentUser.UserId, out var parsedId) ? parsedId : null;

            if (string.IsNullOrWhiteSpace(actorDisplayName) || actorId is null)
            {
                var account = await dbContext.AdminUsers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        user => user.Username == actorUsername,
                        http.RequestAborted);

                actorDisplayName ??= account?.DisplayName;
                actorId ??= account?.Id;
            }

            // a rejected login must not confirm whether the account exists
            var actorName = action == ActivityActions.Login && !succeeded
                ? actorUsername
                : string.IsNullOrWhiteSpace(actorDisplayName)
                    ? actorUsername
                    : actorDisplayName!;

            var entityLabel = BuildEntityLabel(sectionId, body, http);

            var description = ActivityLogNarrator.Describe(
                actorName,
                sectionTitle,
                action,
                entityLabel,
                changedFields,
                succeeded,
                statusCode);

            var entry = new AdminActivityLog(
                actorId,
                actorUsername,
                actorDisplayName,
                action,
                sectionId,
                sectionTitle,
                entityLabel,
                description,
                changedFields,
                http.Request.Method,
                Trim(path, MaxPathLength),
                statusCode,
                succeeded,
                ResolveIpAddress(http),
                Trim(http.Request.Headers.UserAgent.ToString(), MaxUserAgentLength),
                DateTimeOffset.UtcNow);

            dbContext.AdminActivityLogs.Add(entry);
            await dbContext.SaveChangesAsync(http.RequestAborted);
        }
        catch (Exception exception)
        {
            // logging must never take the request down
            _logger.LogWarning(exception, "Failed to write the admin activity log for {Path}", path);
        }
    }

    private static string ResolveAction(string method, string path, RequestBodyInfo body)
    {
        if (path.EndsWith("/login", StringComparison.OrdinalIgnoreCase))
        {
            return ActivityActions.Login;
        }

        if (path.EndsWith("/logout", StringComparison.OrdinalIgnoreCase))
        {
            return ActivityActions.Logout;
        }

        if (method.Equals("GET", StringComparison.OrdinalIgnoreCase))
        {
            return ActivityActions.Export;
        }

        var lowerPath = path.ToLowerInvariant();

        if (lowerPath.Contains("/photos") || lowerPath.StartsWith("/api/admin/uploads"))
        {
            return ActivityActions.Upload;
        }

        if (lowerPath.Contains("/status") ||
            lowerPath.Contains("/toggle") ||
            lowerPath.Contains("/activate") ||
            lowerPath.Contains("/deactivate") ||
            lowerPath.Contains("/cancel") ||
            lowerPath.Contains("/fulfillment") ||
            lowerPath.Contains("/pay") ||
            lowerPath.Contains("/restore"))
        {
            return ActivityActions.Status;
        }

        return method.ToUpperInvariant() switch
        {
            "POST" => ActivityActions.Create,
            "PUT" or "PATCH" => ActivityActions.Update,
            "DELETE" => ActivityActions.Delete,
            _ => ActivityActions.Other,
        };
    }

    private static string? BuildEntityLabel(string sectionId, RequestBodyInfo body, HttpContext http)
    {
        var noun = AdminSections.EntityNounOf(sectionId);

        if (http.Request.Path.Value?.EndsWith("/purge", StringComparison.OrdinalIgnoreCase) == true)
        {
            return "لاگ‌های قدیمی";
        }

        if (!string.IsNullOrWhiteSpace(body.Label))
        {
            return Trim($"{noun} «{body.Label}»", MaxEntityLabelLength);
        }

        if (http.Request.RouteValues.TryGetValue("id", out var routeId) &&
            Guid.TryParse(routeId?.ToString(), out var id))
        {
            return $"{noun} #{id.ToString("N")[..8].ToUpperInvariant()}";
        }

        return null;
    }

    private static string? ResolveIpAddress(HttpContext http)
    {
        var forwarded = http.Request.Headers["X-Forwarded-For"].ToString();

        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            var first = forwarded.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(first))
            {
                return first;
            }
        }

        return http.Connection.RemoteIpAddress?.ToString();
    }

    private static async Task<RequestBodyInfo> ReadBodyAsync(HttpRequest request)
    {
        if (request.ContentType is null ||
            !request.ContentType.Contains("json", StringComparison.OrdinalIgnoreCase) ||
            !request.ContentLength.HasValue ||
            request.ContentLength == 0)
        {
            return RequestBodyInfo.Empty;
        }

        try
        {
            request.EnableBuffering();
            // model binding already consumed the stream — rewind before reading
            request.Body.Position = 0;

            using var reader = new StreamReader(
                request.Body,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                leaveOpen: true);

            var json = await reader.ReadToEndAsync();
            request.Body.Position = 0;

            if (string.IsNullOrWhiteSpace(json))
            {
                return RequestBodyInfo.Empty;
            }

            using var document = JsonDocument.Parse(json);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return RequestBodyInfo.Empty;
            }

            var labels = new List<string>();
            string? username = null;
            string? label = null;

            foreach (var property in document.RootElement.EnumerateObject())
            {
                var name = property.Name;

                if (name.Equals("username", StringComparison.OrdinalIgnoreCase) &&
                    property.Value.ValueKind == JsonValueKind.String)
                {
                    username = property.Value.GetString();
                }

                if (label is null &&
                    LabelCandidateKeys.Contains(name, StringComparer.OrdinalIgnoreCase) &&
                    property.Value.ValueKind == JsonValueKind.String)
                {
                    var value = property.Value.GetString();

                    if (!string.IsNullOrWhiteSpace(value) && value.Length <= 80)
                    {
                        label = value.Trim();
                    }
                }

                if (ActivityFieldLabels.ShouldSkip(name) ||
                    property.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                {
                    continue;
                }

                var labelText = ActivityFieldLabels.LabelFor(name);

                if (!labels.Contains(labelText))
                {
                    labels.Add(labelText);
                }
            }

            return new RequestBodyInfo(labels.Take(12).ToArray(), label, username);
        }
        catch
        {
            return RequestBodyInfo.Empty;
        }
    }

    private static string Trim(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private sealed record RequestBodyInfo(
        IReadOnlyList<string> FieldLabels,
        string? Label,
        string? Username)
    {
        public static RequestBodyInfo Empty { get; } = new(Array.Empty<string>(), null, null);
    }
}
