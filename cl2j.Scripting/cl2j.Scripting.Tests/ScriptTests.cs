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
        public void A_script_exposes_the_options_it_was_created_from()
        {
            var options = ScriptOptions.Create<int>("return 1;");

            var script = Script.Create(options);

            Assert.Same(options, script.Options);
        }

        [Fact]
        public void A_null_context_calls_the_method_that_takes_none()
        {
            //A null passes no argument at all, so the parameterless method answers.
            var script = Script.Create(ScriptOptions.Create<int>("return 7;"));

            Assert.Equal(7, script.Execute<string, int>(null));
        }

        [Fact]
        public void A_script_compiled_with_debug_information_runs_like_any_other()
        {
            var options = ScriptOptions.Create<string, int>("return context.Length;");
            options.CompileWithDebug = true;

            var script = Script.Create(options);

            Assert.Equal(5, script.Execute<string, int>("hello"));
        }

        [Fact]
        public void A_generated_class_that_cannot_be_constructed_is_reported_with_the_cause()
        {
            //A field initializer runs in the constructor, so it fails the construction of the
            //generated class without the code having to name that class.
            var options = ScriptOptions.Create<int>("return 1;");
            options.MethodDeclaration = "private readonly int broken = Fail();\nprivate static int Fail() => throw new InvalidOperationException(\"no instance\");\n" + options.MethodDeclaration;

            var exception = Assert.Throws<ScriptException>(() => Script.Create(options));

            Assert.Contains("Error while creating the generated class instance", exception.Message);
            Assert.Equal("return 1;", exception.CompileSource);
            Assert.NotNull(exception.InnerException);
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

        [Fact]
        public void What_a_script_with_a_context_throws_reaches_the_caller_unwrapped()
        {
            //The path cl2j.Database takes. Wrapped in a TargetInvocationException, a missing column
            //surfaced as "Exception has been thrown by the target of an invocation".
            var script = Script.Create(ScriptOptions.Create<string, int>("throw new InvalidOperationException(\"from the script: \" + context);"));

            var ex = Assert.Throws<InvalidOperationException>(() => script.Execute<string, int>("x"));

            Assert.Equal("from the script: x", ex.Message);
        }

        [Fact]
        public void What_a_script_without_a_context_throws_reaches_the_caller_unwrapped()
        {
            var script = Script.Create(ScriptOptions.Create<int>("throw new InvalidOperationException(\"no context\");"));

            Assert.Throws<InvalidOperationException>(() => script.Execute<int>());
        }

        [Fact]
        public void What_a_script_with_no_return_throws_reaches_the_caller_unwrapped()
        {
            var script = Script.Create(ScriptOptions.Create("throw new InvalidOperationException(\"void\");"));

            Assert.Throws<InvalidOperationException>(() => script.Execute());
        }

        [Fact]
        public void A_script_called_with_a_wider_return_type_still_runs()
        {
            //An int method asked for an object cannot be bound to a Func<int, object>; it falls back
            //to the reflection call, which boxes the result.
            var script = Script.Create(ScriptOptions.Create<int, int>("return context + 1;"));

            Assert.Equal(2, script.Execute<int, object>(1));
            Assert.Equal(3, script.Execute<int, object>(2));
        }

        [Fact]
        public void What_a_script_throws_on_the_fallback_path_reaches_the_caller_unwrapped()
        {
            var script = Script.Create(ScriptOptions.Create<int, int>("throw new InvalidOperationException(\"fallback\");"));

            Assert.Throws<InvalidOperationException>(() => script.Execute<int, object>(1));
        }

        [Fact]
        public void A_script_called_with_two_signatures_answers_both()
        {
            //The bound delegate is a single slot: switching signatures rebinds or falls back, and
            //neither returns the other signature's result.
            var script = Script.Create(ScriptOptions.Create<int, int>("return context * 10;"));

            Assert.Equal(10, script.Execute<int, int>(1));
            Assert.Equal(20, script.Execute<int, object>(2));
            Assert.Equal(30, script.Execute<int, int>(3));
        }

        [Fact]
        public void A_static_script_method_is_called()
        {
            var options = ScriptOptions.Create<int, int>("return context - 1;");
            options.MethodDeclaration = "public static int Execute(int context)";
            var script = Script.Create(options);

            Assert.Equal(4, script.Execute<int, int>(5));
        }

        [Fact]
        public async Task One_script_shared_across_threads_returns_each_caller_its_own_result()
        {
            //cl2j.Database caches one script per type and every query uses it, from any thread.
            var script = Script.Create(ScriptOptions.Create<int, int>("return context * 2;"));

            var results = await Task.WhenAll(Enumerable.Range(0, 64).Select(i => Task.Run(() => script.Execute<int, int>(i))));

            Assert.Equal(Enumerable.Range(0, 64).Select(i => i * 2), results);
        }
    }
}
