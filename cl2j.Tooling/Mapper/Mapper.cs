//using System.Reflection;
//using Microsoft.Extensions.Logging;

//namespace cl2j.Tooling.Mapper
//{
//    public static class Mapper
//    {
//        private readonly static Dictionary<string, List<PropertyInfo>> cacheProperties = new();

//        public static ILogger? Logger { get; set; }

//        public static TDestination Map<TDestination>(this object source)
//            where TDestination : class, new()
//        {
//            var destination = Activator.CreateInstance<TDestination>();
//            source.MapProperties(destination);
//            return destination;
//        }

//        public static void MapProperties<TSource, TDestination>(this TSource source, TDestination destination)
//            where TSource : class, new()
//            where TDestination : class, new()
//        {
//            var sourceProperties = GetProperties(source);
//            var destinationProperties = GetProperties(destination);

//            foreach (var sourceProperty in sourceProperties)
//            {
//                var destinationProperty = destinationProperties.Find(item => item.Name == sourceProperty.Name);
//                if (destinationProperty == null)
//                    continue;

//                try
//                {
//                    destinationProperty.SetValue(destination, sourceProperty.GetValue(source, null), null);
//                }
//                catch (Exception ex)
//                {
//                    Logger?.LogWarning(ex, $"Error occured while mapping property {destinationProperty.Name} of {GetTypeName(destination.GetType())}");
//                }
//            }
//        }

//        private static List<PropertyInfo> GetProperties(object o)
//        {
//            var type = o.GetType();
//            string typeName = GetTypeName(type);

//            if (cacheProperties.TryGetValue(typeName, out var properties))
//                return properties;

//            properties = type.GetProperties().Where(p => p.CanWrite).ToList();
//            cacheProperties.Add(typeName, properties);

//            return properties;
//        }

//        private static string GetTypeName(Type type)
//        {
//            var typeName = type.AssemblyQualifiedName;
//            typeName ??= type.Name;
//            return typeName;
//        }
//    }
//}
