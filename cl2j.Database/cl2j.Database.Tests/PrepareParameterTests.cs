using System.Data;
using cl2j.Database.CommandBuilders;
using cl2j.Database.Descriptors;
using Xunit;

namespace cl2j.Database.Tests
{
    /// <summary>
    ///     <c>ICommandBuilder.PrepareParameter</c> is the provider's one chance to adjust a parameter
    ///     before it travels, and PostgreSQL depends on it (UTC for a <c>DateTimeOffset</c>, a JSON type
    ///     for a JSON column). It is only worth anything if every path that binds a parameter goes
    ///     through it, so each public path is run against a connection that talks to nothing and the
    ///     parameters the provider saw are compared with the ones the path binds.
    /// </summary>
    public class PrepareParameterTests
    {
        private sealed class Harness
        {
            public Harness()
            {
                Builder = new TestProviderCommandBuilder();
                CommandBuilderFactory.Register(Builder);

                Connection = new RecordingDbConnection();
                var shape = new DataTable();
                shape.Columns.Add("Id", typeof(string));
                shape.Columns.Add("Name", typeof(string));
                shape.Columns.Add("Count", typeof(int));
                Connection.ReaderShape = shape;
            }

            public TestProviderCommandBuilder Builder { get; }

            public RecordingDbConnection Connection { get; }

            public string[] PreparedNames => [.. Builder.Prepared.Select(p => p.Parameter.ParameterName)];

            //What a command was given must be exactly what the provider was shown.
            public string[] BoundNames => [.. Connection.Commands.SelectMany(c => c.Parameters.Cast<System.Data.Common.DbParameter>()).Select(p => p.ParameterName)];

            public bool EveryParameterWasShown
                => Connection.Commands
                    .SelectMany(c => c.Parameters.Cast<System.Data.Common.DbParameter>())
                    .SequenceEqual(Builder.Prepared.Select(p => p.Parameter));
        }

        private static readonly TpPrepRow Row = new() { Id = "a", Name = "n", Count = 3 };

        private static void AssertPrepared(Harness harness, params string[] expected)
        {
            Assert.Equal(expected, harness.PreparedNames);
            Assert.Equal(expected, harness.BoundNames);
            Assert.True(harness.EveryParameterWasShown);
        }

        [Fact]
        public async Task Insert_shows_every_parameter_to_the_provider()
        {
            var harness = new Harness();

            await harness.Connection.Insert(Row, CancellationToken.None);

            AssertPrepared(harness, "Id", "Name", "Count");
        }

        [Fact]
        public async Task Insert_with_a_typed_result_shows_every_parameter_to_the_provider()
        {
            var harness = new Harness();

            await harness.Connection.Insert<TpPrepRow, int>(Row);

            AssertPrepared(harness, "Id", "Name", "Count");
        }

        [Fact]
        public async Task Update_shows_every_parameter_to_the_provider()
        {
            var harness = new Harness();

            await harness.Connection.Update(Row, CancellationToken.None);

            AssertPrepared(harness, "Id", "Name", "Count");
        }

        [Fact]
        public async Task Update_of_one_column_shows_the_value_and_the_key_to_the_provider()
        {
            var harness = new Harness();

            await harness.Connection.UpdateColumn(Row, "Name", "other");

            AssertPrepared(harness, "Name", "Id");
        }

        [Fact]
        public async Task Delete_shows_the_key_to_the_provider()
        {
            var harness = new Harness();

            await harness.Connection.Delete(Row, CancellationToken.None);

            AssertPrepared(harness, "Id");
        }

        [Fact]
        public async Task Delete_by_key_shows_the_key_to_the_provider()
        {
            var harness = new Harness();

            await harness.Connection.DeleteKey<TpPrepRow>("a", CancellationToken.None);

            AssertPrepared(harness, "Id");
        }

        [Fact]
        public async Task Query_by_key_shows_the_key_to_the_provider()
        {
            var harness = new Harness();

            await harness.Connection.QueryKey<TpPrepRow>("a", CancellationToken.None);

            AssertPrepared(harness, "Id");
        }

        [Fact]
        public async Task Query_by_keys_shows_every_key_of_every_batch_to_the_provider_with_its_column()
        {
            var harness = new Harness();
            object[] keys = ["a", "b", "c", "d"];

            await harness.Connection.QueryKeys<TpPrepRow>(keys, CancellationToken.None);

            //Three keys to a statement, so the fourth starts a second one.
            AssertPrepared(harness, "key0", "key1", "key2", "key0");
            Assert.All(harness.Builder.Prepared, p => Assert.Equal("Id", p.Column?.Name));
        }

        [Fact]
        public async Task Query_by_example_shows_the_example_to_the_provider()
        {
            var harness = new Harness();

            await harness.Connection.Query<TpPrepRow>(new TpPrepFilter { Name = "n" }, CancellationToken.None);

            AssertPrepared(harness, "Name");
        }

        [Fact]
        public async Task Query_with_sql_shows_the_example_to_the_provider()
        {
            var harness = new Harness();

            await harness.Connection.Query<TpPrepRow>("SELECT 1", new TpPrepFilter { Name = "n" }, CancellationToken.None);

            AssertPrepared(harness, "Name");
        }

        [Fact]
        public async Task Query_single_with_sql_shows_the_example_to_the_provider()
        {
            var harness = new Harness();

            await harness.Connection.QuerySingle<TpPrepRow>("SELECT 1", new TpPrepFilter { Name = "n" }, CancellationToken.None);

            AssertPrepared(harness, "Name");
        }

        [Fact]
        public async Task Query_single_by_example_shows_the_example_to_the_provider()
        {
            var harness = new Harness();

            await harness.Connection.QuerySingle<TpPrepRow>(new TpPrepFilter { Name = "n" }, CancellationToken.None);

            AssertPrepared(harness, "Name");
        }

        [Fact]
        public async Task Execute_shows_the_example_to_the_provider()
        {
            var harness = new Harness();

            await harness.Connection.Execute("DELETE", new TpPrepFilter { Name = "n" }, CancellationToken.None);

            AssertPrepared(harness, "Name");
        }

        [Fact]
        public async Task Table_exists_shows_its_parameters_to_the_provider_without_a_column()
        {
            var harness = new Harness();

            await harness.Connection.TableExists(typeof(TpPrepRow), CancellationToken.None);

            AssertPrepared(harness, "tableName");
            Assert.Null(harness.Builder.Prepared.Single().Column);
        }

        [Fact]
        public async Task The_provider_sees_each_parameter_after_the_core_has_set_its_value()
        {
            var harness = new Harness();

            await harness.Connection.Insert(Row, CancellationToken.None);

            Assert.Equal(new object?[] { "a", "n", 3 }, harness.Builder.ValuesAtPreparation);
        }
    }
}
