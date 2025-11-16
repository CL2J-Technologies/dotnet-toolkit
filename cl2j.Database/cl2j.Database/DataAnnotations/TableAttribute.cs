namespace cl2j.Database.DataAnnotations
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class TableAttribute(string name) : Attribute
    {
        public string? Name { get; set; } = name;
        public string? Schema { get; set; }
    }
}
