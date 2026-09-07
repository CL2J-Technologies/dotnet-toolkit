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

        /// <summary>
        ///     Registers a command builder, replacing one of the same kind if it is already there.
        ///
        ///     <para>
        ///     It used to append with no check at all, so the registry grew for as long as anything
        ///     kept calling — and since resolution returns the first entry that supports the
        ///     connection, every registration after the first was unreachable. A second call
        ///     passing a custom <see cref="IIdentifierGenerator"/> therefore did nothing, silently,
        ///     which makes a parameter that exists to be used easy to lose. See issue #29.
        ///     </para>
        ///
        ///     <para>
        ///     Replacing rather than appending also stops the growth, and the copy-on-write below
        ///     made that growth costly: a new array per call is O(n²) over n registrations. That is
        ///     the right trade for a registry written a handful of times at startup, and the wrong
        ///     one for a registry written in a loop, which nothing prevented.
        ///     </para>
        ///
        ///     <para>
        ///     Kind means the concrete type. A provider package registers one builder type, so two
        ///     instances of it are two configurations of the same provider and the later one wins.
        ///     Builders of different types sit side by side, which is how more than one provider
        ///     works.
        ///     </para>
        /// </summary>
        public static void Register(ICommandBuilder commandBuilder)
        {
            ArgumentNullException.ThrowIfNull(commandBuilder);

            lock (RegistrationLock)
            {
                //A new array each time rather than a mutation, so no reader is ever looking at the
                //one being changed.
                var replaced = builders
                    .Where(b => b.GetType() != commandBuilder.GetType())
                    .Append(commandBuilder)
                    .ToArray();

                Volatile.Write(ref builders, replaced);
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
