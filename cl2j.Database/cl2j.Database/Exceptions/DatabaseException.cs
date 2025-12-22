namespace cl2j.Database.Exceptions
{
    public class DatabaseException : Exception
    {
        public DatabaseException()
        {
        }

        public DatabaseException(string message)
            : base(message)
        {
        }

        public DatabaseException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}
