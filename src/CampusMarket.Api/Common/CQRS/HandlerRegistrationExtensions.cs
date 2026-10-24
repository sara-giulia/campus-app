using System.Reflection;

namespace CampusMarket.Api.Common.CQRS;

public static class HandlerRegistrationExtensions
{
    public static IServiceCollection AddHandlers(this IServiceCollection services, Assembly assembly)
    {
        var handlerInterfaces = new[]
        {
            typeof(ICommandHandler<,>),
            typeof(IQueryHandler<,>)
        };

        foreach (var type in assembly.GetTypes())
        {
            if (type.IsAbstract || type.IsInterface)
                continue;

            foreach (var @interface in type.GetInterfaces())
            {
                if (!@interface.IsGenericType)
                    continue;

                var genericDefinition = @interface.GetGenericTypeDefinition();
                if (Array.IndexOf(handlerInterfaces, genericDefinition) < 0)
                    continue;

                services.AddTransient(@interface, type);
            }
        }

        return services;
    }
}
