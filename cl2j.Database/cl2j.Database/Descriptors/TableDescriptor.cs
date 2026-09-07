namespace cl2j.Database.Descriptors
{
    public class TableDescriptor
    {
        public string Name { get; set; } = null!;
        public string NameFormatted { get; set; } = null!;

        /// <summary>
        ///     The declared schema, or <c>null</c> when the type does not name one. Kept apart from
        ///     <see cref="NameFormatted"/> because a catalog query needs the two separately.
        /// </summary>
        public string? Schema { get; set; }

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
