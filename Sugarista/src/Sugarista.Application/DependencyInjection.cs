using Sugarista.Application.Common.Behaviours;
using Sugarista.Application.Accounting.Services;
using Sugarista.Application.Customers.Services;
using Sugarista.Application.Orders.Commands.ConfirmMockPayment;
using Sugarista.Application.Discounts.Engine;
using Sugarista.Application.Discounts.Engine.Evaluators;
using Sugarista.Application.Discounts.Reports;
using Sugarista.Application.Pickup;

namespace Sugarista.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
        services.AddScoped<IAccountingService, AccountingService>();
        services.AddScoped<ILoyaltyService, LoyaltyService>();
        services.AddScoped<IOrderPaymentFinalizer, OrderPaymentFinalizer>();
        services.AddScoped<IDiscountEngine, DiscountEngine>();
        services.AddScoped<IDiscountEvaluator, AmountOffDiscountEvaluator>();
        services.AddScoped<IDiscountEvaluator, TieredCartDiscountEvaluator>();
        services.AddScoped<IDiscountEvaluator, BuyXGetYDiscountEvaluator>();
        services.AddScoped<DiscountReportBuilder>();
        services.AddScoped<ReportScheduleDispatcher>();
        services.AddScoped<PickupSlotService>();

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(UnhandledExceptionBehaviour<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));
        });

        return services;
    }
}
