namespace cl2j.Database.CommandBuilders.Models
{
    public class TableDescriptor
    {
        public string Name { get; set; } = null!;
        public string NameFormatted { get; set; } = null!;

        public List<ColumnDescriptor> Columns { get; set; } = [];
        public List<ColumnDescriptor> Keys { get; set; } = [];

        public bool IsKey(ColumnDescriptor column)
        {
            return Keys.Any(c => c == column);
        }

        public override string ToString()
        {
            return $"{Name}";
        }
    }
}
