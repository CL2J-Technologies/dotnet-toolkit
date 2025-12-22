namespace cl2j.Database.DataAnnotations
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class ForeignKeyAttribute(string name, string referenceTable, string referenceField, string? referenceSchema = null) : Attribute
    {
        public string Name => name;

        public string ReferenceTable => referenceTable;
        public string? ReferenceSchema => referenceSchema;

        public string ReferenceField => referenceField;
    }
}
