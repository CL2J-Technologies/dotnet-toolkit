using System.Reflection;

namespace cl2j.Smapper
{
    internal class PropertyMap
    {
        public PropertyInfo SourceProperty { get; set; } = null!;
        public PropertyInfo DestinationProperty { get; set; } = null!;
    }
}