using System.Diagnostics;
using System.Reflection;
using cl2j.Scripting.InstanceCreators;
using cl2j.Tooling;
using Microsoft.CodeAnalysis;

namespace cl2j.Scripting
{
    public class ScriptOptions
    {
        private const string DefaultMethodName = "Execute";
        private const string DefaultInputName = "context";

        private int assemblyLoadCountDuplicates;

        public HashSet<string> Namespaces { get; set; } = [];
        public HashSet<PortableExecutableReference> Assemblies { get; set; } = [];

        public bool CompileWithDebug { get; set; }

        public void AddDefault()
        {
            AddNamespaces(
                "System",
                "System.Text",
                "System.Collections",
                "System.Threading.Tasks",
                "System.Linq"
            );

            //AddAssembly(typeof(Script)); // This Library :-)

            var sw = Stopwatch.StartNew();
            AddExecutableAssemblies();
            Console.WriteLine($"ScriptOptions.AddDefault: {Assemblies.Count} Load, {assemblyLoadCountDuplicates} duplicates in {sw.ElapsedMilliseconds}ms");
        }

        public void AddNamespaces(params string[] nameSpaces)
        {
            var list = nameSpaces.Where(ns => !string.IsNullOrEmpty(ns));
            foreach (var l in list)
                Namespaces.Add(l);
        }

        public bool AddAssembly(Type type)
        {
            return AddAssembly(type.Assembly);
        }

        public bool AddAssembly(Assembly assembly)
        {
            var location = assembly.Location;
            if (string.IsNullOrEmpty(location))
                return true;

            try
            {
                if (!Assemblies.Any(a => a.FilePath == location))
                {
                    //Console.WriteLine($"ScriptOptions.AddAssembly('{assembly.FullName}'): {assembly.Location}");
                    var reference = MetadataReference.CreateFromFile(location);
                    Assemblies.Add(reference);
                }
                else
                    ++assemblyLoadCountDuplicates;

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EXCEPTION] ScriptOptions.AddAssemblies: Failed to load assembly {assembly.FullName}: {ex.Message}");
                return false;
            }
        }

        public string Code { get; set; } = string.Empty;

        public string MethodDeclaration { get; set; } = string.Empty;

        public IInstanceCreator InstanceCreator { get; set; } = new InstanceCreator();

        public string? ClassNamespace { get; set; }
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

        #region Private Methods

        private void AddExecutableAssemblies()
        {
            var rootAsembly = Assembly.GetEntryAssembly();
            if (rootAsembly is not null)
            {
                var domainAssemblies = AppDomain.CurrentDomain.GetAssemblies();
                AddAssemblies(domainAssemblies, true);
            }
        }

        private bool AddAssemblies(IEnumerable<Assembly> assemblies, bool recursive = false)
        {
            bool res = true;
            foreach (var assembly in assemblies)
            {
                try
                {
                    res &= AddAssembly(assembly);

                    if (recursive)
                    {
                        try
                        {
                            var referencedAssemblies = assembly.GetReferencedAssemblies();
                            foreach (var reference in referencedAssemblies)
                            {
                                var refAssembly = Assembly.Load(reference);
                                AddAssembly(refAssembly);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[EXCEPTION] ScriptOptions.AddAssemblies: Failed to load referenced assemblies for {assembly.FullName}: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[EXCEPTION] ScriptOptions.AddAssemblies: Failed to load assembly {assembly.FullName}: {ex.Message}");
                }
            }
            return res;
        }

        #endregion Private Methods
    }
}
