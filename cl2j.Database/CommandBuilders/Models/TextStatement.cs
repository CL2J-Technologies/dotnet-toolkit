namespace cl2j.Database.CommandBuilders.Models
{
    public class TextStatement
    {
        public TableDescriptor TableDescriptor { get; set; } = null!;

        public string Text { get; set; } = null!;
    }
}
