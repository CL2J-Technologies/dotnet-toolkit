namespace cl2j.Scripting
{
    public class ScriptException : Exception
    {
        public ScriptException()
        {
        }

        public ScriptException(string message)
            : base(message)
        {
        }

        public ScriptException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}