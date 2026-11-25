using System.Reflection;

namespace cl2j.Smapper
{
    internal sealed class PropertyMap
    {
        public PropertyInfo SourceProperty { get; set; } = null!;
        public PropertyInfo DestinationProperty { get; set; } = null!;
    }
}