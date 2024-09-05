using System.Reflection;
using cl2j.Scripting.Compilers;
using cl2j.Scripting.Generators;

namespace cl2j.Scripting.InstanceResolvers
{
    public class InstanceCreator : IInstanceCreator
    {
        public object CreateInstance(ScriptDeclaration scriptDeclaration, string methodDeclaration, string code)
        {
            //Generate the class source
            var source = ClassGenerator.GenerateSource(methodDeclaration, code, scriptDeclaration.Namespaces);

            //Compile the class source into an assembly
            var compilationResult = ClassCompiler.CompileAssembly(source, scriptDeclaration.References, scriptDeclaration.CompileWithDebug);
            if (compilationResult.Assembly is null)
                throw new ScriptException(compilationResult.ErrorMessage ?? "Unknown error");

            //Create an instance of the class generated
            var fullyQualifiedClassName = source.GetFullyQualifiedClassName();
            try
            {
                var instance = CreateInstance(compilationResult.Assembly, fullyQualifiedClassName)
                    ?? throw new ScriptException($"Unable to instanciate generated class '{fullyQualifiedClassName}'");
                return instance;
            }
            catch (Exception ex)
            {
                throw new ScriptException($"Error while crating the generated class instance '{fullyQualifiedClassName}'", ex);
            }
        }

        private static object? CreateInstance(Assembly assembly, string fullyQualifiedClassName)
        {
            return assembly.CreateInstance(fullyQualifiedClassName);
        }
    }
}
