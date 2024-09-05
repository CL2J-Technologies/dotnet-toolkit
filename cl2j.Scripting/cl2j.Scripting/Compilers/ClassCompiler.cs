using System.Reflection;
using System.Text;
using cl2j.Scripting.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;

namespace cl2j.Scripting.Compilers
{
    public class ClassCompiler
    {
        public static CompilationResult CompileAssembly(ClassSource source, IEnumerable<PortableExecutableReference> references, bool compileWithDebug = false)
        {
            var tree = SyntaxFactory.ParseSyntaxTree(source.Source);

            var optimizationLevel = compileWithDebug ? OptimizationLevel.Debug : OptimizationLevel.Release;
            var compilation = CSharpCompilation.Create(source.ClassName)
                .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: optimizationLevel))
                .AddReferences(references)
                .AddSyntaxTrees(tree);

            var result = new CompilationResult
            {
                GeneratedClass = tree.ToString()
            };

            var compilationStream = new MemoryStream();
            using (compilationStream)
            {
                EmitResult compilationResult;
                if (compileWithDebug)
                    compilationResult = compilation.Emit(compilationStream, options: new EmitOptions(debugInformationFormat: DebugInformationFormat.Embedded));
                else
                    compilationResult = compilation.Emit(compilationStream);
                if (!compilationResult.Success)
                {
                    var sb = new StringBuilder();
                    foreach (var diagnostic in compilationResult.Diagnostics)
                        sb.AppendLine(diagnostic.ToString());
                    result.ErrorMessage = sb.ToString();

                    return result;
                }
            }

            result.Assembly = LoadAssembly(compilationStream.ToArray());

            return result;
        }

        private static Assembly LoadAssembly(byte[] rawAssembly)
        {
            return Assembly.Load(rawAssembly);
        }
    }
}
