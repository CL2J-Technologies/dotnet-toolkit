namespace cl2j.Scripting.Tests
{
    public class EvaluateTests
    {
        [Theory]
        [InlineData(@"""string1""", "string1")]
        public void EvaluateExpression_WithoutModel_Success(string expression, string expected)
        {
            var scriptEngine = ScriptEngine.Create();
            var result = scriptEngine.Execute<string>(ScriptEngine.AddReturn(expression));

            Assert.Equal(expected, result.Result);
        }

        [Theory]
        [InlineData("true", true)]
        [InlineData("false", false)]
        [InlineData(@"context.Text == ""Text1""", true)]
        [InlineData(@"context.Text == ""Text2""", false)]
        [InlineData(@"context.Value > 0", true)]
        [InlineData(@"context.Value == 0", false)]
        public void EvaluateExpression_WithModel_Success(string expression, bool expected)
        {
            var context = new TestScriptContext
            {
                Text = "Text1",
                Value = 5
            };

            var scriptDefinition = ScriptDeclaration.CreateDefault();
            scriptDefinition.AddAssembly(typeof(TestScriptContext));
            var scriptEngine = ScriptEngine.Create(scriptDefinition);
            var result = scriptEngine.Execute<TestScriptContext, bool>(ScriptEngine.AddReturn(expression), context);

            Assert.Equal(expected, result.Result);
        }
    }
}