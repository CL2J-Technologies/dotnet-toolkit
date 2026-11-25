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
            ConnectionExtensions.Logger = ResolveLogger(app);

            var options = app.GetService<IOptions<DatabaseOptions>>();
            if (options is not null)
                ConnectionExtensions.DatabaseOptions = options.Value;
        }

        public static void UseDatabase(this IHost app)
        {
            ConnectionExtensions.Logger = ResolveLogger(app.Services);

            var options = app.Services.GetService<IOptions<DatabaseOptions>>();
            if (options is not null)
                ConnectionExtensions.DatabaseOptions = options.Value;
        }

        /// <summary>
        ///     A standard host registers <see cref="ILoggerFactory"/> and <c>ILogger&lt;T&gt;</c>, never a
        ///     bare <see cref="ILogger"/>: the factory comes first, a registered bare logger is the
        ///     fallback, and without either the library does not log.
        /// </summary>
        private static ILogger? ResolveLogger(IServiceProvider services)
        {
            var factory = services.GetService<ILoggerFactory>();
            return factory is not null
                ? factory.CreateLogger("cl2j.Database")
                : services.GetService<ILogger>();
        }
    }
}
