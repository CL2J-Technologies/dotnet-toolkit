using System.Text;

namespace cl2j.Scripting.Generators
{
    public class ClassGenerator
    {
        public static ClassSource GenerateSource(string methodDeclaration, string code, IEnumerable<string> usings, string classNamespace = "cl2j.Scripting.GeneratedCode")
        {
            var className = $"Class_{Guid.NewGuid():N}";

            var sb = new StringBuilder();

            // Usings
            foreach (var name in usings)
                sb.AppendLine($"using {name};");
            sb.Append(Environment.NewLine);

            // Class Namespace
            sb.Append("namespace " + classNamespace + Environment.NewLine + "{" + Environment.NewLine);
            // Class Definition
            sb.Append($"public class {className}" + Environment.NewLine + "{" + Environment.NewLine);

            // Method
            sb.AppendLine(methodDeclaration + Environment.NewLine + "{" + Environment.NewLine);
            sb.AppendLine(code + Environment.NewLine);
            sb.AppendLine("}" + Environment.NewLine);

            //Close class
            sb.AppendLine("} " + Environment.NewLine);
            //Close namespace
            sb.AppendLine("} " + Environment.NewLine);

            return new ClassSource
            {
                Namespace = classNamespace,
                ClassName = className,
                Source = sb.ToString()
            };
        }
    }
}
