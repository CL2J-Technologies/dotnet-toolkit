namespace cl2j.Smapper
{
    public static class Smapper
    {
        public static TDestination Map<TDestination>(this object source)
            where TDestination : class
        {
            var sourceType = source.GetType();
            var mapper = MapperCache.GetOrCreateMapper(sourceType, typeof(TDestination));

            var destination = Map<TDestination>(source, mapper);
            return destination;
        }

        private static TDestination Map<TDestination>(object source, TypeMapper mapper)
        {
            var destination = Activator.CreateInstance<TDestination>();

            foreach (var propertyMapping in mapper.PropertyMaps)
            {
                var sourceValue = propertyMapping.SourceProperty.GetValue(source, null);
                propertyMapping.DestinationProperty.SetValue(destination, sourceValue, null);
            }

            return destination;
        }
    }
}