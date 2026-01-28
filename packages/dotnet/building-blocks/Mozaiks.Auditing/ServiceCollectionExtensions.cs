using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Mozaiks.Auditing;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMongoAdminAuditing(
        this IServiceCollection services,
        string serviceName,
        Action<AdminAuditOptions>? configure = null,
        bool addIndexInitializer = false)
    {
        services.AddOptions<AdminAuditOptions>()
            .Configure(options =>
            {
                options.ServiceName = serviceName;
                configure?.Invoke(options);
            })
            .Validate(o => !string.IsNullOrWhiteSpace(o.ServiceName), "AdminAuditOptions:ServiceName must be set.")
            .ValidateOnStart();

        services.AddScoped<AdminAuditActionFilter>();

        if (addIndexInitializer)
        {
            services.AddHostedService<AdminAuditIndexInitializer>();
        }

        return services;
    }
}
