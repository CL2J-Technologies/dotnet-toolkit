using cl2j.Scripting.InstanceCreators;
using cl2j.Tooling;
using Microsoft.CodeAnalysis;

namespace cl2j.Scripting
{
    public class ScriptOptions
    {
        private const string DefaultMethodName = "Execute";
        private const string DefaultInputName = "context";

        public List<string> Namespaces { get; set; } = [];
        public HashSet<PortableExecutableReference> Assemblies { get; set; } = [];

        public bool CompileWithDebug { get; set; }

        public void AddDefault()
        {
            AddNamespaces([
                "System",
                "System.Text",
                "System.Collections",
                "System.Threading.Tasks",
                "System.Linq"
            ]);

            AddAssembly(typeof(Script)); // This Library :-)

            AddAssemblies([
                "System.Private.CoreLib.dll",
                "System.Linq.dll"
            ]);
        }

        public void AddNamespaces(params string[] nameSpaces)
        {
            var list = nameSpaces.Where(ns => !string.IsNullOrEmpty(ns));
            Namespaces.AddRange(list);
        }

        public bool AddAssemblies(params string[] assemblies)
        {
            bool res = true;
            foreach (var file in assemblies)
                res &= AddAssembly(file);
            return res;
        }

        public bool AddAssembly(Type type)
        {
            if (string.IsNullOrEmpty(type.Assembly.Location))
                return false;

            try
            {
                if (!Assemblies.Any(r => r.FilePath == type.Assembly.Location))
                {
                    var systemReference = MetadataReference.CreateFromFile(type.Assembly.Location);
                    Assemblies.Add(systemReference);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool AddAssembly(string assemblyDll)
        {
            if (string.IsNullOrEmpty(assemblyDll))
                return true;

            //Check if dll exits in the current execution folder
            var file = Path.GetFullPath(assemblyDll);
            if (!File.Exists(file))
            {
                //Check if dll exists in the .NET runtime folder
                var path = Path.GetDirectoryName(typeof(object).Assembly.Location);
                if (!string.IsNullOrEmpty(path))
                {
                    file = Path.Combine(path, assemblyDll);
                    if (!File.Exists(file))
                        return false;
                }
            }

            if (!Assemblies.Any(r => r.FilePath == file))
            {
                var reference = MetadataReference.CreateFromFile(file);
                Assemblies.Add(reference);
            }

            return true;
        }

        public string Code { get; set; } = string.Empty;

        public string MethodDeclaration { get; set; } = string.Empty;

        public IInstanceCreator InstanceCreator { get; set; } = new InstanceCreator();

        public string MethodName { get; set; } = "Execute";
        public string InputName { get; set; } = "context";

        public static ScriptOptions Create(string code, bool addDefault = true)
        {
            var options = new ScriptOptions
            {
                MethodDeclaration = Method(DefaultMethodName),
                Code = code
            };

            if (addDefault)
                options.AddDefault();

            return options;
        }

        public static ScriptOptions Create<TOut>(string code, bool addDefault = true)
        {
            var options = new ScriptOptions
            {
                MethodDeclaration = Method<TOut>(DefaultMethodName),
                Code = code
            };

            if (addDefault)
                options.AddDefault();

            return options;
        }

        public static ScriptOptions Create<TIn, TOut>(string code, bool addDefault = true)
        {
            var options = new ScriptOptions
            {
                MethodDeclaration = Method<TIn, TOut>(DefaultMethodName, DefaultInputName),
                Code = code,
            };

            if (addDefault)
                options.AddDefault();

            return options;
        }

        public static string Method(string methodName) => $"public void {methodName}()";

        public static string Method<TOut>(string methodName) => $"public {TypeUtils.GetTypeName<TOut>()} {methodName}()";

        public static string Method<TIn, TOut>(string methodName, string inputName) => $"public {TypeUtils.GetTypeName<TOut>()} {methodName}({TypeUtils.GetTypeName<TIn>()} {inputName})";
        public static string AddReturn(string code) => $"return {code};";
    }
}
