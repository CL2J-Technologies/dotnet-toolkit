using cl2j.Database.Descriptors;

namespace cl2j.Database.CommandBuilders
{
    public class TextStatement
    {
        public TableDescriptor TableDescriptor { get; set; } = null!;

        public string Text { get; set; } = null!;

        /// <summary>
        ///     The values <see cref="Text"/> refers to through placeholders. Empty for statements
        ///     whose parameters all derive from the columns of a type.
        /// </summary>
        public IList<StatementParameter> Parameters { get; } = [];
    }
}
