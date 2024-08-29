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