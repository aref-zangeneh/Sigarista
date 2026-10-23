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
using Sugarista.Application;
using Sugarista.Infrastructure;
using Sugarista.Application.Common.Options;
using Sugarista.Infrastructure.Auth;
using Sugarista.Infrastructure.Data;
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

app.UseForwardedHeaders();
app.UseExceptionHandling();

app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers.TryAdd("X-Content-Type-Options", "nosniff");
    headers.TryAdd("X-Frame-Options", "SAMEORIGIN");
    headers.TryAdd("Referrer-Policy", "no-referrer");
    headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=(), payment=()");
    headers.TryAdd("Cross-Origin-Resource-Policy", "same-site");
    headers.TryAdd("Content-Security-Policy", "default-src 'none'; frame-ancestors 'none'; base-uri 'none'");

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

using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<ApplicationDbContextSeeder>();
    await seeder.SeedAsync();

    if (app.Environment.IsProduction())
    {
        var adminOptions = configuration.GetSection(AdminBootstrapOptions.SectionName).Get<AdminBootstrapOptions>();
        if (adminOptions is not null &&
            string.Equals(adminOptions.Password, "ChangeMe123!", StringComparison.Ordinal))
        {
            app.Logger.LogCritical(
                "The bootstrap admin password is still the public default. Change it from the admin panel or set AdminBootstrap__Password before exposing this installation.");
        }
    }
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

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsRoot),
    RequestPath = "/api/files",
});

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
