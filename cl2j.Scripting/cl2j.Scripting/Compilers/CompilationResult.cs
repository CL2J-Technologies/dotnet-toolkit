using System.Reflection;

namespace cl2j.Scripting.Compilers
{
    public class CompilationResult
    {
        public Assembly? Assembly { get; set; }
        public string? GeneratedClass { get; set; }

        public string? ErrorMessage { get; set; }
    }
}
