using cl2j.Scripting.InstanceCreators;
using Microsoft.CodeAnalysis;
using Xunit;

namespace cl2j.Scripting.Tests
{
    /// <summary>The settings of <see cref="ScriptOptions"/> that a caller replaces wholesale.</summary>
    public class ScriptOptionsSettingsTests
    {
        private sealed class StubCreator : IInstanceCreator
        {
            public object CreateInstance(ScriptOptions options) => new object();
        }

        [Fact]
        public void Every_setting_reads_back_what_was_assigned()
        {
            var creator = new StubCreator();
            var reference = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);

            var options = new ScriptOptions
            {
                Namespaces = ["My.Namespace"],
                Assemblies = [reference],
                CompileWithDebug = true,
                InstanceCreator = creator,
                ClassNamespace = "Generated",
                MethodName = "Run",
                InputName = "input",
            };

            Assert.Equal(["My.Namespace"], options.Namespaces);
            Assert.Same(reference, Assert.Single(options.Assemblies));
            Assert.True(options.CompileWithDebug);
            Assert.Same(creator, options.InstanceCreator);
            Assert.Equal("Generated", options.ClassNamespace);
            Assert.Equal("Run", options.MethodName);
            Assert.Equal("input", options.InputName);
        }
    }
}
