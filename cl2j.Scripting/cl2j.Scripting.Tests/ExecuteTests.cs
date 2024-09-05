namespace cl2j.Scripting.Tests
{
    public class ExecuteTests
    {
        [Fact]
        public void ExecuteConsole_WithoutModel_Success()
        {
            var expression = @"Debug.WriteLine(""Test"");";

            var scriptDefinition = ScriptDeclaration.CreateDefault();
            scriptDefinition.AddNamespaces("System.Diagnostics");
            var scriptEngine = ScriptEngine.Create(scriptDefinition);
            scriptEngine.Execute(expression);
        }

        [Fact]
        public void Execute_WithCache_Success()
        {
            var expression = @"Debug.WriteLine(""Test"");";

            var scriptDefinition = ScriptDeclaration.CreateDefault();
            scriptDefinition.AddNamespaces("System.Diagnostics");
            var scriptEngine = ScriptEngine.Create(scriptDefinition);
            scriptEngine.Execute(expression);
        }

        [Fact]
        public void ExecuteExpressionSimple_WithoutModel_Success()
        {
            var expression = @"context.Text == ""Text1""";

            var context = new TestScriptContext
            {
                Text = "Text1",
                Value = 5
            };

            var scriptDefinition = ScriptDeclaration.CreateDefault();
            scriptDefinition.AddAssembly(typeof(TestScriptContext));
            var scriptEngine = ScriptEngine.Create(scriptDefinition);
            var result = scriptEngine.Execute<TestScriptContext, bool>(ScriptEngine.AddReturn(expression), context);

            Assert.True(result.Result);
        }
    }
}