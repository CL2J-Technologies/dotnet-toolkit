using cl2j.Scripting.InstanceResolvers;
using Microsoft.Extensions.DependencyInjection;

namespace cl2j.Scripting
{
    public static class ScriptingExtensions
    {
        public static void AddScripting(this IServiceCollection services, bool enableCaching = true)
        {
            if (enableCaching)
            {
                services.AddSingleton<IInstanceCreator>(o =>
                {
                    var decorator = new InstanceCreator();
                    return new InstanceCreatorCache(decorator);
                });
            }
            else
                services.AddSingleton<IInstanceCreator, InstanceCreator>();
        }
    }
}
