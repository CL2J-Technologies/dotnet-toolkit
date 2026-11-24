using System.Reflection;
using System.Reflection.Emit;
using cl2j.Scripting.InstanceCreators;
using Xunit;

namespace cl2j.Scripting.Tests
{
    /// <summary>
    ///     The options a script is compiled with: the method it is wrapped in, and the namespaces
    ///     and assemblies it is allowed to see.
    /// </summary>
    public class ScriptOptionsTests
    {
        /// <summary>An assembly that claims a file which is not there, so a reference to it cannot be built.</summary>
        private sealed class MissingFileAssembly : Assembly
        {
            public override string Location => Path.Combine(Path.GetTempPath(), "cl2j-no-such-assembly", "missing.dll");

            public override string? FullName => "MissingFileAssembly, Version=1.0.0.0";
        }

        [Fact]
        public void A_new_set_of_options_is_empty_and_uses_the_default_names()
        {
            var options = new ScriptOptions();

            Assert.Empty(options.Namespaces);
            Assert.Empty(options.Assemblies);
            Assert.False(options.CompileWithDebug);
            Assert.Equal(string.Empty, options.Code);
            Assert.Equal(string.Empty, options.MethodDeclaration);
            Assert.Null(options.ClassNamespace);
            Assert.Equal("Execute", options.MethodName);
            Assert.Equal("context", options.InputName);
            Assert.IsType<InstanceCreator>(options.InstanceCreator);
        }

        [Fact]
        public void Namespaces_are_added_without_duplicates_or_blanks()
        {
            var options = new ScriptOptions();

            options.AddNamespaces("System", "System.Text", "System", "", null!);

            Assert.Equal(["System", "System.Text"], options.Namespaces.OrderBy(n => n, StringComparer.Ordinal));
        }

        [Fact]
        public void An_assembly_is_added_through_one_of_its_types()
        {
            var options = new ScriptOptions();

            Assert.True(options.AddAssembly(typeof(ScriptOptionsTests)));

            Assert.Single(options.Assemblies);
        }

        [Fact]
        public void The_same_assembly_added_twice_is_kept_once()
        {
            var options = new ScriptOptions();

            Assert.True(options.AddAssembly(typeof(ScriptOptionsTests).Assembly));
            Assert.True(options.AddAssembly(typeof(ScriptOptionsTests)));

            Assert.Single(options.Assemblies);
        }

        [Fact]
        public void Two_options_share_the_reference_built_for_one_assembly()
        {
            var first = new ScriptOptions();
            var second = new ScriptOptions();

            first.AddAssembly(typeof(ScriptOptionsTests));
            second.AddAssembly(typeof(ScriptOptionsTests));

            Assert.Same(first.Assemblies.Single(), second.Assemblies.Single());
        }

        [Fact]
        public void An_assembly_with_no_file_is_accepted_and_left_out()
        {
            var dynamicAssembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("cl2j.Scripting.Tests.Dynamic"), AssemblyBuilderAccess.Run);
            var options = new ScriptOptions();

            Assert.True(options.AddAssembly(dynamicAssembly));

            Assert.Empty(options.Assemblies);
        }

        [Fact]
        public void An_assembly_whose_file_cannot_be_read_is_reported_as_not_added()
        {
            var options = new ScriptOptions();

            Assert.False(options.AddAssembly(new MissingFileAssembly()));

            Assert.Empty(options.Assemblies);
        }

        [Fact]
        public void The_defaults_bring_the_usual_namespaces_and_the_loaded_assemblies()
        {
            var options = new ScriptOptions();

            options.AddDefault();

            Assert.Superset(new HashSet<string> { "System", "System.Text", "System.Collections", "System.Threading.Tasks", "System.Linq" }, options.Namespaces);
            Assert.NotEmpty(options.Assemblies);
        }

        [Fact]
        public void Create_without_defaults_adds_nothing()
        {
            var options = ScriptOptions.Create("var x = 1;", addDefault: false);

            Assert.Equal("var x = 1;", options.Code);
            Assert.Equal("public void Execute()", options.MethodDeclaration);
            Assert.Empty(options.Namespaces);
            Assert.Empty(options.Assemblies);
        }

        [Fact]
        public void Create_with_defaults_adds_them()
        {
            var options = ScriptOptions.Create("var x = 1;");

            Assert.NotEmpty(options.Namespaces);
            Assert.NotEmpty(options.Assemblies);
        }

        [Fact]
        public void Create_with_an_output_type_declares_a_method_returning_it()
        {
            var options = ScriptOptions.Create<int>("return 1;", addDefault: false);

            Assert.Equal("return 1;", options.Code);
            Assert.Equal("public System.Int32 Execute()", options.MethodDeclaration);
            Assert.Empty(options.Assemblies);
        }

        [Fact]
        public void Create_with_an_input_and_an_output_type_declares_a_method_taking_context()
        {
            var options = ScriptOptions.Create<string, int>("return context.Length;", addDefault: false);

            Assert.Equal("return context.Length;", options.Code);
            Assert.Equal("public System.Int32 Execute(System.String context)", options.MethodDeclaration);
            Assert.Empty(options.Assemblies);
        }

        [Fact]
        public void The_typed_creators_add_the_defaults_unless_told_not_to()
        {
            Assert.NotEmpty(ScriptOptions.Create<int>("return 1;").Assemblies);
            Assert.NotEmpty(ScriptOptions.Create<string, int>("return 1;").Assemblies);
        }

        [Fact]
        public void Method_declarations_are_built_from_a_name_and_the_types()
        {
            Assert.Equal("public void Run()", ScriptOptions.Method("Run"));
            Assert.Equal("public System.Int32 Run()", ScriptOptions.Method<int>("Run"));
            Assert.Equal("public System.String Run(System.Int32 value)", ScriptOptions.Method<int, string>("Run", "value"));
        }

        [Fact]
        public void A_return_wraps_an_expression()
        {
            Assert.Equal("return a + b;", ScriptOptions.AddReturn("a + b"));
        }
    }
}
