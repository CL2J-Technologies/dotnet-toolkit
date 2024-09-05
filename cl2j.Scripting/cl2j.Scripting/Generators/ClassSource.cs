namespace cl2j.Scripting.Generators
{
    public class ClassSource
    {
        public string Namespace { get; set; } = null!;
        public string ClassName { get; set; } = null!;

        public string Source { get; set; } = null!;

        public string GetFullyQualifiedClassName()
        {
            return Namespace + "." + ClassName;
        }
    }
}
