using System.Reflection;
using cl2j.Database.DataAnnotations;

namespace cl2j.Database.Helpers
{
    public static class PropertyHelpers
    {
        public static IEnumerable<PropertyInfo> GetTableProperties(Type type)
        {
            var properties = type.GetProperties().Where(x => x.CanRead && !x.HasAttribute<IgnoreAttribute>());
            return properties;
        }
    }
}
