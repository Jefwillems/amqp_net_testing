using Messaging.Amqp.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Messaging.Amqp.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMessaging(this IServiceCollection services, string configSection)
    {
        services.AddOptions<MessagingOptions>()
            .BindConfiguration(configSection);
        return services;
    }
}