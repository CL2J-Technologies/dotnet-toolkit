using System.Reflection;

namespace cl2j.Database.Helpers
{
    public static class AttributeHelpers
    {
        public static TAttribute? GetAttribute<TAttribute>(this Type type)
            where TAttribute : class
        {
            var attr = Attribute.GetCustomAttribute(type, typeof(TAttribute));
            return attr as TAttribute;
        }

        public static TAttribute? GetAttribute<TAttribute>(this PropertyInfo propertyInfo)
            where TAttribute : class
        {
            var attr = Attribute.GetCustomAttribute(propertyInfo, typeof(TAttribute));
            return attr as TAttribute;
        }

        public static bool HasAttribute<TAttribute>(this PropertyInfo propertyInfo)
            where TAttribute : class
        {
            var attr = Attribute.GetCustomAttribute(propertyInfo, typeof(TAttribute));
            return attr as TAttribute is not null;
        }
    }
}
