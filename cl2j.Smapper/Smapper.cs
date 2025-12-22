using System.Text.Json;

namespace cl2j.Smapper
{
    public static class Smapper
    {
        public static TDestination Map<TDestination>(this object source, TDestination? input = null)
            where TDestination : class
        {
            var sourceType = source.GetType();
            var mapper = MapperCache.GetOrCreateMapper(sourceType, typeof(TDestination));

            var destination = Map(source, mapper, input);
            return destination;
        }

        public static TSource? Clone<TSource>(this TSource source)
            where TSource : class
        {
            var data = JsonSerializer.Serialize(source);
            var cloned = JsonSerializer.Deserialize<TSource>(data);
            return cloned;
        }

        private static TDestination Map<TDestination>(object source, TypeMapper mapper, TDestination? input)
        {
            var destination = input ?? Activator.CreateInstance<TDestination>();

            foreach (var propertyMapping in mapper.PropertyMaps)
            {
                var sourceValue = propertyMapping.SourceProperty.GetValue(source, null);
                propertyMapping.DestinationProperty.SetValue(destination, sourceValue, null);
            }

            return destination;
        }
    }
}