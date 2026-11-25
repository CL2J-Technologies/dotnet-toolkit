namespace cl2j.Tooling.Exceptions
{
    public class UnhandledException : Exception
    {
        public UnhandledException()
        {
        }

        public UnhandledException(string message)
            : base(message)
        {
        }

        public UnhandledException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}