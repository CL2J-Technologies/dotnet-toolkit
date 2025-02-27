using cl2j.Database.CommandBuilders;
using cl2j.Database.Databases;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace cl2j.Database
{
    public static class DatabaseExtensions
    {
        public static IServiceCollection AddDatabase(this IServiceCollection services, Action<IServiceCollection>? configure = null)
        {
            services.AddOptions<DatabaseOptions>();

            services.TryAddSingleton<ICommandBuilderFactory, CommandBuilderFactory>();

            configure?.Invoke(services);

            return services;
        }
    }
}
