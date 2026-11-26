using System.Reflection;

namespace cl2j.Smapper
{
    public class ObjectExtensions
    {
        public static IEnumerable<PropertyInfo> GetReadableProperties(Type type)
        {
            return type.GetProperties().Where(x => x.CanRead);
        }

        public static IEnumerable<PropertyInfo> GetReadableWritableProperties(Type type)
        {
            return type.GetProperties().Where(x => x.CanRead && x.CanWrite);
        }

        [Obsolete("Use GetReadableWritableProperties. The misspelled name will be removed in the next major version.")]
        public static IEnumerable<PropertyInfo> GetReadaleWritableProperties(Type type)
        {
            return GetReadableWritableProperties(type);
        }
    }
}