using cl2j.Database.Descriptors;

namespace cl2j.Database.CommandBuilders
{
    public class TextStatement
    {
        public TableDescriptor TableDescriptor { get; set; } = null!;

        public string Text { get; set; } = null!;
    }
}
