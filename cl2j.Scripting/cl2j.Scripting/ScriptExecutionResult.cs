namespace cl2j.Scripting
{
    public class ScriptExecutionResult
    {
        public ScriptErrorTypes ErrorType { get; set; } = ScriptErrorTypes.None;
        public Exception? Exception { get; set; }
    }

    public class ScriptExecutionResult<T> : ScriptExecutionResult
    {
        public T? Result { get; set; }
    }

    public enum ScriptErrorTypes
    {
        None = 0,
        Compilation,
        Execution,
    }
}
