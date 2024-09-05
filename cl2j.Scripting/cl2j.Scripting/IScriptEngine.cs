namespace cl2j.Scripting
{
    public interface IScriptEngine
    {
        ScriptExecutionResult<TOut> Execute<TIn, TOut>(string code, TIn context);
        ScriptExecutionResult<TOut> Execute<TOut>(string code);
    }
}