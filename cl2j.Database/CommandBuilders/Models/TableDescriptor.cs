namespace cl2j.Database.CommandBuilders.Models
{
    public class TableDescriptor
    {
        public string Name { get; set; } = null!;
        public string NameFormatted { get; set; } = null!;

        public List<ColumnDescriptor> Columns { get; set; } = [];
        public List<ColumnDescriptor> Keys { get; set; } = [];

        public List<ColumnDescriptor> GetColumnsWithoutKey()
        {
            var list = new List<ColumnDescriptor>(Columns.Count);
            foreach (var c in Columns)
            {
                if (c.ColumnAtribute.Key != DataAnnotations.KeyType.Key)
                    list.Add(c);
            }
            return list;
        }

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
