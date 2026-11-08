using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Sugarista.Api.Middleware;
using Sugarista.Api.Authorization;
using Sugarista.Api.Auth;
using Sugarista.Application;
using Sugarista.Infrastructure;
using Sugarista.Application.Common.Options;
using Sugarista.Infrastructure.Auth;
using Sugarista.Infrastructure.Data;
using Sugarista.Infrastructure.Data.DbConnection;
using Sugarista.Infrastructure.Settings;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

builder.Services.AddApplication();
builder.Services.AddInfrastructure(configuration);

builder.Services.AddScoped<Sugarista.Api.Filters.ActivityLogFilter>();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    static string ClientKey(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    options.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(ClientKey(context), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));

    options.AddPolicy("checkout", context =>
        RateLimitPartition.GetFixedWindowLimiter(ClientKey(context), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 30,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));

    options.AddPolicy("uploads", context =>
        RateLimitPartition.GetFixedWindowLimiter(ClientKey(context), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 60,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // The API container is only reachable through the bundled nginx reverse proxy.
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddControllers(options =>
    {
        options.Filters.Add<Sugarista.Api.Filters.ActivityLogFilter>();
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddCors(options =>
{
    options.AddPolicy("AppCors", policy =>
    {
        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("JWT configuration is missing.");

if (builder.Environment.IsProduction())
{
    var signingKey = jwtOptions.SigningKey ?? string.Empty;
    var looksLikePlaceholder =
        signingKey.Contains("ChangeMe", StringComparison.OrdinalIgnoreCase) ||
        signingKey.Contains("ChangeInProduction", StringComparison.OrdinalIgnoreCase) ||
        signingKey.Contains("ReplaceWith", StringComparison.OrdinalIgnoreCase);

    if (signingKey.Length < 32 || looksLikePlaceholder)
    {
        throw new InvalidOperationException(
            "JWT signing key is missing or insecure. Set Jwt__SigningKey to a random secret of at least 32 characters before running in Production.");
    }
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey!)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

var app = builder.Build();

var dbConnectionManager = app.Services.GetRequiredService<DbConnectionManager>();

app.UseForwardedHeaders();
app.UseExceptionHandling();
app.UseMiddleware<PrintAgentKeyMiddleware>();

app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers.TryAdd("X-Content-Type-Options", "nosniff");
    headers.TryAdd("X-Frame-Options", "SAMEORIGIN");
    headers.TryAdd("Referrer-Policy", "no-referrer");
    headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=(), payment=()");
    headers.TryAdd("Cross-Origin-Resource-Policy", "same-site");

    // The first-run setup page needs inline scripts/styles; everything else
    // keeps the strict CSP. The page itself is only reachable while unconfigured.
    if (context.Request.Path.StartsWithSegments("/api/setup"))
    {
        headers.TryAdd("Content-Security-Policy",
            "default-src 'self'; style-src 'self' 'unsafe-inline'; script-src 'self' 'unsafe-inline'; img-src 'self' data:; frame-ancestors 'none'");
    }
    else
    {
        headers.TryAdd("Content-Security-Policy", "default-src 'none'; frame-ancestors 'none'; base-uri 'none'");
    }

    if (context.Request.IsHttps)
    {
        headers.TryAdd("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
    }

    if (context.Request.Path.StartsWithSegments("/api") &&
        !context.Request.Path.StartsWithSegments("/api/files"))
    {
        headers.TryAdd("Cache-Control", "no-store");
    }

    await next();
});

// WordPress-style first-run gate: while the database is unconfigured, every
// request is blocked except /api/setup (page + endpoints) and /health.
app.UseMiddleware<SetupGateMiddleware>();

if (dbConnectionManager.IsConfigured)
{
    var seeded = false;

    try
    {
        using (var scope = app.Services.CreateScope())
        {
            await scope.ServiceProvider
                .GetRequiredService<ApplicationDbContextSeeder>()
                .SeedAsync();
        }

        LogAdminBootstrapWarning(app, app.Logger);
        seeded = true;
    }
    catch (Exception exception)
    {
        // Database exists in config but is unreachable right now (e.g. postgres
        // is still starting). Never crash the app: the site keeps answering and
        // migrations/seeding retry in the background.
        app.Logger.LogError(
            exception,
            "Initial database migration/seed failed — the database will be retried in the background.");
    }

    if (!seeded)
    {
        _ = RunBackgroundSeederAsync(app);
    }
}
else
{
    app.Logger.LogWarning(
        "Database is not configured yet. Starting in SETUP MODE: only /api/setup and /health respond. " +
        "Complete the first-run setup at /api/setup, or edit {ConfigPath}.",
        dbConnectionManager.ConfigFilePath);

    _ = RunBackgroundSeederAsync(app);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseHttpsRedirection();
}

app.UseCors("AppCors");
app.UseRateLimiter();

var fileStorageOptions = app.Configuration.GetSection(FileStorageOptions.SectionName).Get<FileStorageOptions>()
    ?? new FileStorageOptions();
var uploadsRoot = Path.IsPathRooted(fileStorageOptions.UploadRootPath)
    ? fileStorageOptions.UploadRootPath
    : Path.Combine(app.Environment.ContentRootPath, fileStorageOptions.UploadRootPath);
Directory.CreateDirectory(Path.Combine(uploadsRoot, "inventory-documents"));
Directory.CreateDirectory(Path.Combine(uploadsRoot, "asset-photos"));
Directory.CreateDirectory(Path.Combine(uploadsRoot, "images"));

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsRoot),
    RequestPath = "/api/files",
    OnPrepareResponse = static context =>
    {
        // Image filenames are generated GUIDs and are never rewritten in place,
        // so the served content is immutable — cache aggressively.
        if (context.Context.Request.Path.Value?.Contains("/images/", StringComparison.OrdinalIgnoreCase) == true)
        {
            context.Context.Response.Headers.CacheControl =
                $"public, max-age={TimeSpan.FromDays(365).TotalSeconds:0}, immutable";
        }
    },
});

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

// ── Background database migration/seed ──────────────────────────────────────
// Waits until the database is configured (setup mode) and/or reachable, then
// runs the same migration + seed that a normal start would have done.
static async Task RunBackgroundSeederAsync(WebApplication app)
{
    var manager = app.Services.GetRequiredService<DbConnectionManager>();
    var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("DbSetup");

    try
    {
        var configured = await manager.WaitUntilConfiguredAsync(app.Lifetime.ApplicationStopping);
        if (!configured)
        {
            return; // shutdown while unconfigured
        }

        logger.LogInformation("Database configured — running migrations and seeding in the background.");

        // Making the app live must not wait for the seeder: requests are handled
        // in parallel while these retries run. Give DB startup a moment.
        await Task.Delay(TimeSpan.FromSeconds(5), app.Lifetime.ApplicationStopping);

        while (!app.Lifetime.ApplicationStopping.IsCancellationRequested)
        {
            try
            {
                using var scope = app.Services.CreateScope();
                await scope.ServiceProvider
                    .GetRequiredService<ApplicationDbContextSeeder>()
                    .SeedAsync(app.Lifetime.ApplicationStopping);

                logger.LogInformation("Database migrations and seeding completed automatically.");

                LogAdminBootstrapWarning(app, logger);

                return;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(
                    exception,
                    "Database is not reachable yet — retrying migration/seed in 5 seconds.");

                await Task.Delay(TimeSpan.FromSeconds(5), app.Lifetime.ApplicationStopping);
            }
        }
    }
    catch (OperationCanceledException)
    {
    }
    catch (Exception exception)
    {
        logger.LogError(
            exception,
            "Background database migration/seed failed. Restart the app to retry.");
    }
}

static void LogAdminBootstrapWarning(WebApplication app, object? logger = null)
{
    ILogger target = logger is ILogger concrete
        ? concrete
        : app.Logger;

    if (!app.Environment.IsProduction())
    {
        return;
    }

    var adminOptions = app.Configuration
        .GetSection(AdminBootstrapOptions.SectionName)
        .Get<AdminBootstrapOptions>();

    if (adminOptions is not null &&
        string.Equals(adminOptions.Password, "ChangeMe123!", StringComparison.Ordinal))
    {
        target.LogCritical(
            "The bootstrap admin password is still the public default. Change it from the admin panel or set AdminBootstrap__Password before exposing this installation.");
    }
}
