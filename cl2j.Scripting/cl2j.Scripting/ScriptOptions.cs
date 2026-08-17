using System.Collections.Concurrent;
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

        // One reference per assembly file, for the life of the process. MetadataReference.CreateFromFile
        // memory-maps the file it is given, and the compilation it is handed to holds that mapping for as
        // long as the compiled script lives — which, for a cached script, is the life of the process.
        // Creating a fresh reference per compilation therefore kept a whole copy of the assembly set
        // alive per script: 58 MB a compilation in a console, ~135 MB in an ASP.NET host with three
        // hundred assemblies loaded. None of it is on the managed heap, so no collection ever gives it
        // back — twenty compiled scripts and the process is holding gigabytes of the same metadata.
        //
        // The files do not change while the process runs, so one reference shared across every
        // compilation is both correct and what Roslyn asks for: it also lets it reuse metadata it has
        // already read instead of parsing each assembly again.
        private static readonly ConcurrentDictionary<string, PortableExecutableReference> ReferenceCache = new(StringComparer.Ordinal);

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
                // Shared, so the set can be a plain reference-equality Add: two references built from
                // one file used to compare unequal, which is why this had to scan the whole set by path
                // on every one of the several thousand assemblies an AddDefault walks through.
                var reference = ReferenceCache.GetOrAdd(location, static path => MetadataReference.CreateFromFile(path));

                if (!Assemblies.Add(reference))
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
