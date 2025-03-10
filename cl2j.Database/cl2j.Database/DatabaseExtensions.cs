using cl2j.Database.Databases;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace cl2j.Database
{
    public static class DatabaseExtensions
    {
        public static IServiceCollection AddDatabase(this IServiceCollection services, Action<DatabaseOptions>? databaseOptions = null, Action<IServiceCollection>? register = null)
        {
            if (databaseOptions is null)
                services.AddOptions<DatabaseOptions>();
            else
                services.Configure(databaseOptions);

            register?.Invoke(services);

            return services;
        }

        public static void UseDatabase(this ServiceProvider app)
        {
            ConnectionExtensions.Logger = app.GetService<ILogger>();

            var options = app.GetService<IOptions<DatabaseOptions>>();
            if (options is not null)
                ConnectionExtensions.DatabaseOptions = options.Value;
        }

        public static void UseDatabase(this IHost app)
        {
            ConnectionExtensions.Logger = app.Services.GetService<ILogger>();

            var options = app.Services.GetService<IOptions<DatabaseOptions>>();
            if (options is not null)
                ConnectionExtensions.DatabaseOptions = options.Value;
        }
    }
}
