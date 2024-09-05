using Microsoft.CodeAnalysis;

namespace cl2j.Scripting
{
    public class ScriptDeclaration
    {
        public List<string> Namespaces { get; set; } = [];
        public HashSet<PortableExecutableReference> References { get; set; } = [];

        public bool CompileWithDebug { get; set; }

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
                if (!References.Any(r => r.FilePath == type.Assembly.Location))
                {
                    var systemReference = MetadataReference.CreateFromFile(type.Assembly.Location);
                    References.Add(systemReference);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static ScriptDeclaration CreateDefault()
        {
            var definition = new ScriptDeclaration();

            definition.AddNamespaces([
                "System",
                "System.Text",
                "System.Collections",
                "System.Threading.Tasks",
                "System.Linq"
            ]);

            definition.AddAssembly(typeof(ScriptEngine)); // This Library :-)

            definition.AddAssemblies([
                "System.Private.CoreLib.dll",
                "System.Linq.dll"
            ]);

            definition.CompileWithDebug = false;

            return definition;
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

            if (!References.Any(r => r.FilePath == file))
            {
                var reference = MetadataReference.CreateFromFile(file);
                References.Add(reference);
            }

            return true;
        }
    }
}
