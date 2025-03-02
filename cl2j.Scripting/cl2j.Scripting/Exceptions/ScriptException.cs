namespace cl2j.Scripting.Exceptions
{
    public class ScriptException : Exception
    {
        public string CompileSource { get; set; } = string.Empty;

        public ScriptException()
        {
        }

        public ScriptException(string message, string source)
            : base(message)
        {
            CompileSource = source;
        }

        public ScriptException(string message, string source, Exception inner) : base(message, inner)
        {
            CompileSource = source;
        }
    }
}