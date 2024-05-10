using System.Reflection;

namespace cl2j.Smapper
{
    public class ObjectExtensions
    {
        public static IEnumerable<PropertyInfo> GetReadableProperties(Type type)
        {
            return type.GetProperties().Where(x => x.CanRead);
        }

        public static IEnumerable<PropertyInfo> GetReadaleWritableProperties(Type type)
        {
            return type.GetProperties().Where(x => x.CanRead && x.CanWrite);
        }
    }
}