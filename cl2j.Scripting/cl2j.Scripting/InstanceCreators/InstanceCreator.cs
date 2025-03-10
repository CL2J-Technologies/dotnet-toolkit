using System.Reflection;
using cl2j.Scripting.Compilers;
using cl2j.Scripting.Exceptions;
using cl2j.Scripting.Generators;

namespace cl2j.Scripting.InstanceCreators
{
    public class InstanceCreator : IInstanceCreator
    {
        public object CreateInstance(ScriptOptions options)
        {
            //Generate the class source
            var source = ClassGenerator.GenerateSource(options.MethodDeclaration, options.Code, options.Namespaces, options.ClassNamespace);

            //Compile the class source into an assembly
            var compilationResult = ClassCompiler.CompileAssembly(source, options);
            if (compilationResult.Assembly is null)
                throw new ScriptException(compilationResult.ErrorMessage ?? "Unknown error", source.Source);

            //Create an instance of the class generated
            var fullyQualifiedClassName = source.GetFullyQualifiedClassName();
            try
            {
                var instance = CreateInstance(compilationResult.Assembly, fullyQualifiedClassName)
                    ?? throw new ScriptException($"Unable to instanciate generated class '{fullyQualifiedClassName}'", options.Code);
                return instance;
            }
            catch (Exception ex)
            {
                throw new ScriptException($"Error while creating the generated class instance '{fullyQualifiedClassName}'", options.Code, ex);
            }
        }

        private static object? CreateInstance(Assembly assembly, string fullyQualifiedClassName)
        {
            return assembly.CreateInstance(fullyQualifiedClassName);
        }
    }
}
