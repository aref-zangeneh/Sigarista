using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Affiliates.Portal;
using Sugarista.Application.Affiliates.Services;
using Sugarista.Application.Carts.Services;
using Sugarista.Application.Customers.Services;
using Sugarista.Application.Exports;
using Sugarista.Application.Maintenance;
using Sugarista.Application.MoqAlerts;
using Sugarista.Application.Pickup;
using Sugarista.Application.Sms;
using Sugarista.Application.Common.Options;
using Sugarista.Infrastructure.Notifications;
using Sugarista.Infrastructure.Auth;
using Sugarista.Infrastructure.Data;
using Sugarista.Infrastructure.Data.DbConnection;
using Sugarista.Infrastructure.Data.Interceptors;
using Sugarista.Infrastructure.Logging;
using Sugarista.Infrastructure.Payments;
using Sugarista.Infrastructure.Printing;
using Sugarista.Infrastructure.Settings;
using Sugarista.Infrastructure.Services;
using Sugarista.Infrastructure.Sms;

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
        services.AddSingleton<IMoqAlertBroadcaster, MoqAlertBroadcaster>();
        services.AddScoped<MoqStockChangeInterceptor>();
        services.AddScoped<ApplicationDbContextSeeder>();

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<AdminBootstrapOptions>(configuration.GetSection(AdminBootstrapOptions.SectionName));
        services.Configure<PaymentOptions>(configuration.GetSection(PaymentOptions.SectionName));
        services.Configure<FileStorageOptions>(configuration.GetSection(FileStorageOptions.SectionName));
        services.Configure<ImageProcessingOptions>(configuration.GetSection(ImageProcessingOptions.SectionName));
        services.Configure<SmsOptions>(configuration.GetSection(SmsOptions.SectionName));
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        services.AddScoped<MaintenanceReminderDispatcher>();
        services.AddScoped<CustomerWinBackDispatcher>();
        services.AddScoped<AbandonedCartReminderDispatcher>();
        services.AddScoped<IAffiliateTierEngine, AffiliateTierEngine>();
        services.AddScoped<IAffiliateTrackingService, AffiliateTrackingService>();
        services.AddScoped<IAffiliateCommissionLifecycleService, AffiliateCommissionLifecycleService>();
        services.AddScoped<IAffiliateNotificationService, AffiliateNotificationService>();
        services.AddScoped<IAffiliatePortalService, AffiliatePortalService>();
        services.AddSingleton<IAffiliateQrCodeService, AffiliateQrCodeService>();
        services.AddScoped<IPaymentGateway, MockPaymentGateway>();
        services.AddScoped<IPrintJobService, ReceiptPrintJobService>();
        services.AddScoped<IPickupSlotReservationService, PickupSlotReservationService>();
        services.AddScoped<IFileStorageService, LocalFileStorageService>();
        services.AddScoped<IImageProcessingService, SkiaSharpImageProcessingService>();
        services.AddScoped<IImageFileStore, LocalImageFileStore>();
        services.AddSingleton<IPublicOriginProvider, PublicOriginProvider>();
        // IAuditLog is registered as the bootstrap's configured instance in Program.cs
        services.AddScoped<IExcelExporter, ClosedXmlExcelExporter>();
        services.AddSingleton<IExportJobQueue, ExportJobQueue>();
        services.AddScoped<IExportFileStore, LocalFileStorageService>();
        services.AddScoped<ExportDataBuilder>();
        services.AddHostedService<ExportJobRunnerService>();
        services.AddHttpClient<KavenegarSmsService>();
        services.AddHttpClient<KavenegarMaintenanceSmsService>();
        services.AddHttpClient<Sugarista.Application.AiContent.IGeminiContentService, GeminiContentService>();
        services.AddScoped<LoggingSmsService>();
        services.AddScoped<LoggingMaintenanceSmsService>();
        services.AddScoped<LoggingMaintenanceEmailService>();
        services.AddScoped<SmtpMaintenanceEmailService>();
        // Legacy channels route through the SMS Management Center when a DB
        // provider exists; otherwise the appsettings-driven services keep working.
        services.AddScoped<SmsGatewayRouter>();
        services.AddScoped<ISmsService>(provider => provider.GetRequiredService<SmsGatewayRouter>());
        services.AddScoped<IMaintenanceSmsSender>(provider => provider.GetRequiredService<SmsGatewayRouter>());
        services.AddScoped<IMaintenanceEmailSender>(provider =>
        {
            var emailOptions = provider.GetRequiredService<IOptions<EmailOptions>>().Value;
            if (string.Equals(emailOptions.Provider, "Smtp", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(emailOptions.Host))
            {
                return provider.GetRequiredService<SmtpMaintenanceEmailService>();
            }

            return provider.GetRequiredService<LoggingMaintenanceEmailService>();
        });
        services.AddHostedService<MaintenanceReminderTickerService>();
        services.AddHostedService<CustomerWinBackTickerService>();
        services.AddHostedService<AffiliateTierEvaluationTickerService>();
        services.AddHostedService<AbandonedCartTickerService>();
        services.AddHostedService<ReportScheduleRunnerService>();
        // Daily-ish pruning of old system log files (LoggingInfrastructure:FileRetentionDays).
        services.AddHostedService(sp => new SystemLogRetentionService(
            sp.GetRequiredService<SystemLogFileStore>(),
            configuration.GetSection(LoggingInfrastructureOptions.SectionName)
                .Get<LoggingInfrastructureOptions>() ?? new LoggingInfrastructureOptions(),
            sp.GetRequiredService<ILogger<SystemLogRetentionService>>()));

        var fallbackConnectionString = configuration.GetConnectionString("DefaultConnection");

        // WordPress-style database setup: the app owns its connection file
        // (config/db-connection.json) written by the first-run /setup page.
        // With no file and no connection string the app starts in "setup mode" —
        // everything is blocked except /api/setup and /health until configured.
        services.AddSingleton<DbConfigStore>();
        services.AddSingleton<AppSetupStore>();
        services.AddSingleton(_ => new DbConnectionManager(
            _.GetRequiredService<DbConfigStore>(),
            fallbackConnectionString,
            _.GetRequiredService<ILogger<DbConnectionManager>>()));

        services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
        {
            var manager = serviceProvider.GetRequiredService<DbConnectionManager>();
            var dataSource = manager.GetDataSource();

            // Setup mode: no provider is attached — requests never reach the
            // context because the setup gate middleware blocks them first.
            if (dataSource is not null)
            {
                options.UseNpgsql(
                    dataSource,
                    builder => builder.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName));
            }

            options.ConfigureWarnings(warnings =>
                warnings.Ignore(RelationalEventId.PendingModelChangesWarning));

            options.AddInterceptors(
                serviceProvider.GetRequiredService<AuditableEntityInterceptor>(),
                serviceProvider.GetRequiredService<MoqStockChangeInterceptor>());
        });

        services.AddScoped<IApplicationDbContext>(provider =>
            provider.GetRequiredService<ApplicationDbContext>());

        // ---- SMS Management Center --------------------------------------
        // Credential keys persist in the config directory (same volume as
        // db-connection.json) so encrypted provider credentials survive
        // container rebuilds.
        services.AddHttpClient();
        var dbConfigPath = configuration["DB_CONFIG_PATH"];
        var configDirectory = string.IsNullOrWhiteSpace(dbConfigPath)
            ? Path.Combine(
                configuration[HostDefaults.ContentRootKey] is { Length: > 0 } contentRoot
                    ? contentRoot
                    : Directory.GetCurrentDirectory(),
                "config")
            : Path.GetDirectoryName(Path.GetFullPath(dbConfigPath))!;
        Directory.CreateDirectory(configDirectory);
        services
            .AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(configDirectory))
            .SetApplicationName("Sugarista");
        services.AddSingleton<ISmsCredentialProtector, SmsCredentialProtector>();
        services.AddSingleton<ISmsSettingsStore, AppSetupSmsSettingsStore>();
        services.AddScoped<ISmsProviderResolver, SmsProviderResolver>();
        services.AddScoped<ISmsProviderGatewayFactory, SmsProviderGatewayFactory>();
        services.AddScoped<ISmsGateway, SmsGateway>();
        services.AddHostedService<SmsCampaignRunnerService>();

        return services;
    }
}
