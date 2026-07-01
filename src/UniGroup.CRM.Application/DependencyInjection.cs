using Microsoft.Extensions.DependencyInjection;

namespace UniGroup.CRM.Application;

/// <summary>
/// Extension methods for registering Application services.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds all application services including MediatR handlers.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
        });

        return services;
    }
}
