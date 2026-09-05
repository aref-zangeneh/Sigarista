using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Common.Options;
using Sugarista.Infrastructure.Notifications;
using Sugarista.Infrastructure.Auth;
using Sugarista.Infrastructure.Data;
using Sugarista.Infrastructure.Data.Interceptors;
using Sugarista.Infrastructure.Payments;
using Sugarista.Infrastructure.Settings;
using Sugarista.Infrastructure.Services;

namespace Sugarista.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddSingleton<IDateTime, DateTimeService>();
        services.AddScoped<IAdminPasswordService, AdminPasswordService>();
        services.AddScoped<IAdminAuthorizationService, AdminAuthorizationService>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<AuditableEntityInterceptor>();
        services.AddScoped<ApplicationDbContextSeeder>();

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<AdminBootstrapOptions>(configuration.GetSection(AdminBootstrapOptions.SectionName));
        services.Configure<PaymentOptions>(configuration.GetSection(PaymentOptions.SectionName));
        services.Configure<FileStorageOptions>(configuration.GetSection(FileStorageOptions.SectionName));
        services.Configure<SmsOptions>(configuration.GetSection(SmsOptions.SectionName));
        services.AddScoped<IPaymentGateway, MockPaymentGateway>();
        services.AddScoped<IFileStorageService, LocalFileStorageService>();
        services.AddHttpClient<KavenegarSmsService>();
        services.AddScoped<LoggingSmsService>();
        services.AddScoped<ISmsService>(provider =>
        {
            var smsOptions = provider.GetRequiredService<IOptions<SmsOptions>>().Value;
            if (string.Equals(smsOptions.Provider, "Kavenegar", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(smsOptions.ApiKey))
            {
                return provider.GetRequiredService<KavenegarSmsService>();
            }

            return provider.GetRequiredService<LoggingSmsService>();
        });

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
        {
            options.UseNpgsql(
                connectionString,
                builder => builder.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName));

            options.ConfigureWarnings(warnings =>
                warnings.Ignore(RelationalEventId.PendingModelChangesWarning));

            options.AddInterceptors(serviceProvider.GetRequiredService<AuditableEntityInterceptor>());
        });

        services.AddScoped<IApplicationDbContext>(provider =>
            provider.GetRequiredService<ApplicationDbContext>());

        return services;
    }
}
