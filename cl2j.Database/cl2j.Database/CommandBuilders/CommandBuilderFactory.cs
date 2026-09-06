using System.Data.Common;
using cl2j.Database.Exceptions;

namespace cl2j.Database.CommandBuilders
{
    /// <summary>
    ///     The process-wide registry of command builders.
    ///
    ///     <para>
    ///     Registration is copy-on-write: <see cref="Register"/> publishes a new array under a
    ///     lock, and <see cref="GetCommandBuilder"/> reads the reference once and walks that
    ///     snapshot. Readers therefore take no lock, which suits a registry read on every database
    ///     call and written to a handful of times at startup.
    ///     </para>
    ///
    ///     <para>
    ///     It used to be a plain <c>List</c> walked with <c>foreach</c>. Because <c>Register</c> is
    ///     public and static, nothing confines it to startup, and a registration landing during a
    ///     resolve threw <c>InvalidOperationException: Collection was modified</c> — from the
    ///     resolve, not from the registration, so the stack pointed at the innocent party. A test
    ///     covers it.
    ///     </para>
    /// </summary>
    public class CommandBuilderFactory
    {
        private static readonly Lock RegistrationLock = new();

        private static ICommandBuilder[] builders = [];

        public static void Register(ICommandBuilder commandBuilder)
        {
            lock (RegistrationLock)
            {
                //A new array each time rather than a mutation, so no reader is ever looking at the
                //one being changed.
                Volatile.Write(ref builders, [.. builders, commandBuilder]);
            }
        }

        public static ICommandBuilder GetCommandBuilder(DbConnection connection)
        {
            //Read once. The array this points at is never modified after it is published, so the
            //walk below is safe however many registrations happen during it.
            var snapshot = Volatile.Read(ref builders);

            foreach (var builder in snapshot)
            {
                if (builder.Support(connection))
                    return builder;
            }

            throw new DatabaseException($"No CommandBuilder registered for connection '{connection.GetType().Name}'");
        }
    }
}
