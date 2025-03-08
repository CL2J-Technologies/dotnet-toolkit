namespace cl2j.Database.DataAnnotations
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class TableAttribute : Attribute
    {
        public TableAttribute(string name)
        {
            Name = name;
        }

        public string? Name { get; set; }
        public string? Schema { get; set; }
    }
}
