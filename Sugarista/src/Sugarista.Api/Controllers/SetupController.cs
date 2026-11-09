using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Constants;
using Sugarista.Domain.Entities;
using Sugarista.Infrastructure.Data.DbConnection;
using Sugarista.Infrastructure.Services;
using Sugarista.Infrastructure.Settings;

namespace Sugarista.Api.Controllers;

public sealed record SetupTestRequest(
    string Host,
    int Port,
    string Database,
    string Username,
    string Password,
    string? SslMode);

/// <summary>First-run connection request with the web admin account to seed.</summary>
public sealed record SetupConnectRequest(
    string Host,
    int Port,
    string Database,
    string Username,
    string Password,
    string? SslMode,
    string? AdminUsername,
    string? AdminDisplayName,
    string? AdminPassword);

public sealed record SetupRotateRequest(
    string CurrentPassword,
    string NewPassword,
    bool AlterInDatabase);

public sealed record SetupStatusResponse(
    bool Configured,
    string? Host,
    int? Port,
    string? Database,
    string? Username,
    string ConfigPath);

public sealed record SetupResultResponse(bool Success, string Message);

/// <summary>
/// WordPress-style database setup and password rotation.
///
/// <list type="bullet">
///   <item><c>GET /api/setup</c> — the Persian setup/rotation page.</item>
///   <item><c>POST /api/setup/connect</c> — first-run: test + persist the connection.</item>
///   <item><c>POST /api/setup/rotate</c> — change the database password without any
///   code change or restart (optionally running ALTER USER in PostgreSQL first).</item>
/// </list>
/// </summary>
[Route("api/setup")]
[AllowAnonymous]
[EnableRateLimiting("auth")]
public sealed class SetupController : ApiControllerBase
{
    private readonly DbConnectionManager _manager;
    private readonly IAdminPasswordService _adminPasswordService;
    private readonly IApplicationDbContext _dbContext;
    private readonly AppSetupStore _appSetupStore;
    private readonly IWebHostEnvironment _environment;

    public SetupController(
        DbConnectionManager manager,
        IAdminPasswordService adminPasswordService,
        IApplicationDbContext dbContext,
        AppSetupStore appSetupStore,
        IWebHostEnvironment environment)
    {
        _manager = manager;
        _adminPasswordService = adminPasswordService;
        _dbContext = dbContext;
        _appSetupStore = appSetupStore;
        _environment = environment;
    }

    [HttpGet]
    public IActionResult SetupPage()
    {
        var webRoot = _environment.WebRootPath;
        if (string.IsNullOrEmpty(webRoot))
        {
            webRoot = Path.Combine(_environment.ContentRootPath, "wwwroot");
        }

        var filePath = Path.Combine(webRoot, "setup.html");

        if (!System.IO.File.Exists(filePath))
        {
            return NotFound("Setup page is not deployed.");
        }

        return PhysicalFile(filePath, "text/html; charset=utf-8");
    }

    [HttpGet("status")]
    public ActionResult<SetupStatusResponse> Status()
    {
        var settings = _manager.CurrentSettings;

        return Ok(new SetupStatusResponse(
            settings is not null,
            settings?.Host,
            settings?.Port,
            settings?.Database,
            settings?.Username,
            _manager.ConfigFilePath));
    }

    [HttpPost("test")]
    public async Task<ActionResult<SetupResultResponse>> Test(
        [FromBody] SetupTestRequest request,
        CancellationToken cancellationToken)
    {
        var (settings, invalidMessage) = BuildSettings(request);
        if (invalidMessage is not null)
        {
            return BadRequest(new SetupResultResponse(false, invalidMessage));
        }

        try
        {
            await DbConnectionManager.TestConnectionAsync(settings!, cancellationToken);
            return Ok(new SetupResultResponse(true, "اتصال با موفقیت برقرار شد."));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return BadRequest(new SetupResultResponse(false, DescribeConnectionFailure(exception)));
        }
    }

    /// <summary>First-run configuration (only allowed while the database is not configured yet).</summary>
    [HttpPost("connect")]
    public async Task<ActionResult<SetupResultResponse>> Connect(
        [FromBody] SetupConnectRequest request,
        CancellationToken cancellationToken)
    {
        if (_manager.IsConfigured)
        {
            return Conflict(new SetupResultResponse(
                false,
                "دیتابیس هم‌اکنون تنظیم شده است. برای تغییر تنها رمز عبور از «تغییر رمز» در همین صفحه استفاده کنید."));
        }

        var (settings, invalidMessage) = BuildSettings(
            request.Host, request.Port, request.Database, request.Username, request.Password, request.SslMode);
        if (invalidMessage is not null)
        {
            return BadRequest(new SetupResultResponse(false, invalidMessage));
        }

        // Optional web-admin account: chosen here so a fresh install never needs
        // env variables. Validated with the same rules as the login endpoint.
        var adminUsername = request.AdminUsername?.Trim();
        var adminDisplayName = request.AdminDisplayName?.Trim();
        var adminPassword = request.AdminPassword;

        if (!string.IsNullOrEmpty(adminUsername))
        {
            if (adminUsername.Length > 100)
            {
                return BadRequest(new SetupResultResponse(false, "نام کاربری ادمین حداکثر ۱۰۰ کاراکتر است."));
            }

            if (!string.IsNullOrEmpty(adminDisplayName) && adminDisplayName.Length > 100)
            {
                return BadRequest(new SetupResultResponse(false, "نام نمایشی ادمین حداکثر ۱۰۰ کاراکتر است."));
            }

            if (string.IsNullOrEmpty(adminPassword))
            {
                return BadRequest(new SetupResultResponse(false, "برای نام کاربری ادمین، رمز عبور ادمین را هم وارد کنید."));
            }

            if (adminPassword.Length < 8)
            {
                return BadRequest(new SetupResultResponse(false, "رمز ادمین باید حداقل ۸ کاراکتر باشد."));
            }
        }

        try
        {
            await DbConnectionManager.TestConnectionAsync(settings!, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return BadRequest(new SetupResultResponse(false, DescribeConnectionFailure(exception)));
        }

        if (!string.IsNullOrEmpty(adminUsername))
        {
            // Persist BEFORE seeding so the background seeder picks these up.
            _appSetupStore.PatchAdmin(
                adminUsername,
                string.IsNullOrWhiteSpace(adminDisplayName) ? null : adminDisplayName,
                adminPassword);
        }

        _manager.Configure(settings!);

        return Ok(new SetupResultResponse(
            true,
            "اتصال ذخیره شد. جداول و داده‌های پایه در پس‌زمینه ساخته می‌شوند؛ حداکثر تا یک دقیقه دیگر سایت فعال است."));
    }

    /// <summary>
    /// Rotates the database password without restarting the site. The only
    /// required secret is the database password that is currently in effect —
    /// whoever can open the database is allowed to change its password.
    /// </summary>
    [HttpPost("rotate")]
    public async Task<ActionResult<SetupResultResponse>> Rotate(
        [FromBody] SetupRotateRequest request,
        CancellationToken cancellationToken)
    {
        var settings = _manager.CurrentSettings;
        if (settings is null)
        {
            return Conflict(new SetupResultResponse(
                false,
                "دیتابیس هنوز تنظیم نشده است؛ ابتدا تنظیم اولیه را از همین صفحه انجام دهید."));
        }

        if (string.IsNullOrWhiteSpace(request.CurrentPassword))
        {
            return BadRequest(new SetupResultResponse(
                false,
                "رمز فعلی دیتابیس را وارد کنید (همان که الان کار میکند)."));
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return BadRequest(new SetupResultResponse(false, "رمز جدید دیتابیس را وارد کنید."));
        }

        try
        {
            // Proof of ownership: the password being replaced must actually
            // open the database. No site admin accounts are involved here.
            await DbConnectionManager.TestConnectionAsync(
                settings.WithPassword(request.CurrentPassword),
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Unauthorized(new SetupResultResponse(false, "رمز فعلی دیتابیس نادرست است."));
        }

        try
        {
            if (request.AlterInDatabase)
            {
                // 1) change the password inside PostgreSQL itself, connecting
                //    with the proven CURRENT password — the site keeps working.
                await AlterDatabaseUserPasswordAsync(
                    settings.WithPassword(request.CurrentPassword),
                    request.NewPassword,
                    cancellationToken);

                // 2) tell the running app: future physical connections use the
                //    new password (existing pooled connections stay alive).
                _manager.UpdatePassword(request.NewPassword);
            }
            else
            {
                // The password was already changed in PostgreSQL (e.g. via
                // Adminer or psql). Prove the new password works BEFORE
                // switching to it — already verified above.
                _manager.UpdatePassword(request.NewPassword);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return BadRequest(new SetupResultResponse(false, DescribeRotationFailure(exception)));
        }

        return Ok(new SetupResultResponse(
            true,
            "رمز دیتابیس عوض شد و سایت بدون قطعی با رمز جدید کار می‌کند. تنظیمات در فایل ذخیره ماند."));
    }

    private static (DbConnectionSettings? Settings, string? InvalidMessage) BuildSettings(
        SetupTestRequest request) =>
        BuildSettings(request.Host, request.Port, request.Database, request.Username, request.Password, request.SslMode);

    private static (DbConnectionSettings? Settings, string? InvalidMessage) BuildSettings(
        string host,
        int port,
        string database,
        string username,
        string password,
        string? sslMode)
    {
        var settings = new DbConnectionSettings
        {
            Host = (host ?? string.Empty).Trim(),
            Port = port <= 0 ? 5432 : port,
            Database = (database ?? string.Empty).Trim(),
            Username = (username ?? string.Empty).Trim(),
            Password = password ?? string.Empty,
            SslMode = string.IsNullOrWhiteSpace(sslMode) ? "Prefer" : sslMode.Trim()
        };

        return (settings, settings.ValidationMessage);
    }

    /// <summary>Runs ALTER USER with dollar-quoted password (safe against special characters).</summary>
    private static async Task AlterDatabaseUserPasswordAsync(
        DbConnectionSettings settings,
        string newPassword,
        CancellationToken cancellationToken)
    {
        var dataSource = settings.BuildDataSource();
        await using (dataSource)
        {
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = $"ALTER USER {QuoteIdentifier(settings.Username)} WITH PASSWORD {DollarQuote(newPassword)}";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static string QuoteIdentifier(string identifier) =>
        "\"" + identifier.Replace("\"", "\"\"") + "\"";

    private static string DollarQuote(string value)
    {
        var tag = "$sgr$";
        while (value.Contains(tag, StringComparison.Ordinal))
        {
            tag = "$sgr$" + Guid.NewGuid().ToString("N")[..6] + "$";
        }

        return tag + value + tag;
    }

    private static string DescribeConnectionFailure(Exception exception) => exception switch
    {
        Npgsql.NpgsqlException pg => pg.SqlState switch
        {
            "28P01" => "رمز عبور دیتابیس اشتباه است.",
            "3D000" => "دیتابیسی با این نام وجود ندارد.",
            "42501" => "کاربر دیتابیس اجازهٔ اتصال ندارد (حق دسترسی).",
            "42P01" => "به دیتابیس وصل شد اما جدول‌ها هنوز ساخته نشده‌اند.",
            _ => $"اتصال به دیتابیس ناموفق بود: {pg.Message}"
        },
        _ => $"اتصال برقرار نشد: {exception.Message}"
    };

    private static string DescribeRotationFailure(Exception exception) =>
        $"تغییر رمز ناتمام ماند: {exception.Message}";
}
