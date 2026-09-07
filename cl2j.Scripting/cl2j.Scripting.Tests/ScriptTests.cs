using cl2j.Scripting;
using cl2j.Scripting.Exceptions;
using Xunit;

namespace cl2j.Scripting.Tests
{
    /// <summary>
    ///     Compiling a fragment of C# at runtime and calling it.
    ///
    ///     <para>
    ///     This package had 68% of its lines executed and not one assertion about any of them: it
    ///     is exercised because <c>cl2j.Database</c>'s reader uses it to compile the mapping code
    ///     it generates per type. Covered is not tested — a regression here would have shown up as
    ///     a failure in the database suite, saying nothing about the cause.
    ///     </para>
    ///
    ///     <para>
    ///     The tests that were here before described a <c>ScriptEngine</c> that no longer exists.
    ///     See issue #22.
    ///     </para>
    /// </summary>
    public class ScriptTests
    {
        [Fact]
        public void A_script_returns_what_its_code_returns()
        {
            var script = Script.Create(ScriptOptions.Create<string>("return \"from the script\";"));

            Assert.Equal("from the script", script.Execute<string>());
        }

        [Fact]
        public void An_expression_can_be_turned_into_a_return()
        {
            //AddReturn is what a caller wraps an expression in when the fragment is meant to be a
            //value rather than a body.
            var script = Script.Create(ScriptOptions.Create<int>(ScriptOptions.AddReturn("2 + 3")));

            Assert.Equal(5, script.Execute<int>());
        }

        [Fact]
        public void A_script_reads_the_context_it_is_given()
        {
            var script = Script.Create(ScriptOptions.Create<int, int>("return context * 2;"));

            Assert.Equal(42, script.Execute<int, int>(21));
        }

        [Fact]
        public void A_script_can_take_a_reference_type_as_its_context()
        {
            //The shape cl2j.Database uses: a DbDataReader in, a list out.
            var script = Script.Create(ScriptOptions.Create<string, int>("return context.Length;"));

            Assert.Equal(5, script.Execute<string, int>("hello"));
        }

        [Fact]
        public void A_script_with_no_return_runs()
        {
            var script = Script.Create(ScriptOptions.Create("var unused = 1 + 1;"));

            script.Execute();
        }

        [Fact]
        public void Code_that_does_not_compile_is_refused_with_the_compiler_message()
        {
            //The failure has to name what the compiler objected to. The generated source is
            //machine-written, so an error without it is unreadable.
            var exception = Assert.Throws<ScriptException>(
                () => Script.Create(ScriptOptions.Create<int>("return \"a string, not an int\";")));

            Assert.Contains("CS0029", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void A_refusal_carries_the_source_that_was_compiled()
        {
            var exception = Assert.Throws<ScriptException>(
                () => Script.Create(ScriptOptions.Create<int>("this is not C#;")));

            Assert.Contains("this is not C#", exception.CompileSource, StringComparison.Ordinal);
        }

        [Fact]
        public void A_namespace_that_was_not_added_is_not_in_scope()
        {
            //Namespaces are opt-in, which is why AddNamespaces exists. Without it the fragment sees
            //only what AddDefault brought in.
            var options = ScriptOptions.Create<string>("return System.Text.Json.JsonSerializer.Serialize(1);", addDefault: false);

            Assert.Throws<ScriptException>(() => Script.Create(options));
        }

        [Fact]
        public void A_namespace_that_was_added_is_in_scope()
        {
            var options = ScriptOptions.Create<string>("return string.Join(\",\", new[] { \"a\", \"b\" });");
            options.AddNamespaces("System.Linq");

            var script = Script.Create(options);

            Assert.Equal("a,b", script.Execute<string>());
        }

        [Fact]
        public void Two_scripts_compiled_from_the_same_code_are_separate_classes()
        {
            //Each compilation gets a generated class name of its own, so one script never resolves
            //to another's type.
            var first = Script.Create(ScriptOptions.Create<int>("return 1;"));
            var second = Script.Create(ScriptOptions.Create<int>("return 2;"));

            Assert.Equal(1, first.Execute<int>());
            Assert.Equal(2, second.Execute<int>());
        }
    }
}
