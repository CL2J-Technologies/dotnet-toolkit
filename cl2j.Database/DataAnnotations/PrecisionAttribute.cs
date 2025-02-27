namespace cl2j.Database.DataAnnotations
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class PrecisionAttribute(short number, short precision) : Attribute
    {
        public short Number => number;
        public short Precision => precision;
    }
}
