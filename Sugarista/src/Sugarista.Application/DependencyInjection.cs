using Sugarista.Application.Common.Behaviours;
using Sugarista.Application.Accounting.Services;
using Sugarista.Application.Customers.Services;
using Sugarista.Application.Discounts.Engine;
using Sugarista.Application.Discounts.Engine.Evaluators;

namespace Sugarista.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
        services.AddScoped<IAccountingService, AccountingService>();
        services.AddScoped<ILoyaltyService, LoyaltyService>();
        services.AddScoped<IDiscountEngine, DiscountEngine>();
        services.AddScoped<IDiscountEvaluator, AmountOffDiscountEvaluator>();
        services.AddScoped<IDiscountEvaluator, TieredCartDiscountEvaluator>();
        services.AddScoped<IDiscountEvaluator, BuyXGetYDiscountEvaluator>();

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(UnhandledExceptionBehaviour<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));
        });

        return services;
    }
}
