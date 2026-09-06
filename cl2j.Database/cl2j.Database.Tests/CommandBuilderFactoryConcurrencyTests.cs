using System.Data;
using System.Data.Common;
using cl2j.Database.CommandBuilders;
using cl2j.Database.Exceptions;
using Xunit;

namespace cl2j.Database.Tests
{
    /// <summary>
    ///     <see cref="CommandBuilderFactory"/> is a process-wide registry: a public static
    ///     <c>Register</c> writing to a list, and a resolve that walks it. Registration is not
    ///     confined to startup — <c>SqlServer.Register</c> can be called from anywhere, at any
    ///     time — so a write can land while another thread is enumerating.
    ///
    ///     <para>
    ///     This test was not written from theory. The suite began failing only once all of its
    ///     classes ran together, with <c>InvalidOperationException</c> where a
    ///     <c>DatabaseException</c> was expected: xUnit runs test classes in parallel, one
    ///     registered while another resolved, and the enumeration threw.
    ///     </para>
    ///
    ///     <para>
    ///     The resolve here uses a connection nothing supports, on purpose. Resolving a supported
    ///     one returns on the first entry and barely enumerates; an unsupported one walks the whole
    ///     list every time, which is where the window actually is.
    ///     </para>
    /// </summary>
    public class CommandBuilderFactoryConcurrencyTests
    {
        [Fact]
        public void Registering_while_resolving_does_not_break_the_registry()
        {
            var failures = new List<Exception>();
            var stop = false;

            var resolvers = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
            {
                while (!Volatile.Read(ref stop))
                {
                    try
                    {
                        CommandBuilderFactory.GetCommandBuilder(new UnsupportedConnection());
                    }
                    catch (DatabaseException)
                    {
                        //"Nothing supports this connection" is the correct answer here.
                    }
                    catch (Exception ex)
                    {
                        //Anything else is the collection being mutated mid-enumeration.
                        lock (failures)
                            failures.Add(ex);
                        return;
                    }
                }
            })).ToArray();

            for (var i = 0; i < 20_000; i++)
                cl2j.Database.SqlServer.SqlServer.Register();

            Volatile.Write(ref stop, true);
            Task.WaitAll(resolvers, TimeSpan.FromSeconds(30));

            Assert.Empty(failures);
        }
    }
}
