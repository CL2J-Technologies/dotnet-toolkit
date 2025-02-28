using cl2j.Database.CommandBuilders;
using cl2j.Database.Databases;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace cl2j.Database
{
    public static class DatabaseExtensions
    {
        public static IServiceCollection AddDatabase(this IServiceCollection services, Action<DatabaseOptions>? databaseOptions = null)
        {
            if (databaseOptions is null)
                services.AddOptions<DatabaseOptions>();
            else
                services.Configure(databaseOptions);

            CommandBuilderFactory.Register(new SqlServerCommandBuilder());

            return services;
        }

        public static void UseDatabase(this ServiceProvider app)
        {
            ConnectionExtensions.Logger = app.GetService<ILogger>();

            var options = app.GetService<IOptions<DatabaseOptions>>();
            if (options is not null)
                ConnectionExtensions.DatabaseOptions = options.Value;
        }
    }
}
