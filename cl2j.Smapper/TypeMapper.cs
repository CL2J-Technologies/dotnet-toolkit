using System.Diagnostics;

namespace cl2j.Smapper
{
    internal class TypeMapper
    {
        public List<PropertyMap> PropertyMaps { get; set; } = new();

        public static TypeMapper Create(Type source, Type destination)
        {
            var mapper = new TypeMapper();

            var sourceProperties = ObjectExtensions.GetReadableProperties(source);
            var destinationProperties = ObjectExtensions.GetReadaleWritableProperties(destination);

            foreach (var destinationProperty in destinationProperties)
            {
                foreach (var sourceProperty in sourceProperties)
                {
                    if (sourceProperty.Name == destinationProperty.Name)
                    {
                        if (sourceProperty.PropertyType == destinationProperty.PropertyType)
                        {
                            var sourceType = sourceProperty.PropertyType;
                            if (sourceType.IsValueType || sourceType == typeof(string))
                                mapper.PropertyMaps.Add(new PropertyMap { SourceProperty = sourceProperty, DestinationProperty = destinationProperty });
                            else
                                Debug.WriteLine($"{sourceProperty.Name} not mapped because it's not a ValueType ({sourceType})");
                        }
                        break;
                    }
                }
            }

            return mapper;
        }
    }
}