using System.Reflection;

namespace cl2j.Database.Helpers
{
    public static class PropertyHelpers
    {
        public static IEnumerable<PropertyInfo> GetTableProperties(Type type)
        {
            return type.GetProperties().Where(x => x.CanRead && x.CanWrite);
        }
    }
}
