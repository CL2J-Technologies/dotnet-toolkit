using System.Data.Common;
using cl2j.Database.Exceptions;
using Npgsql;
using Xunit;

namespace cl2j.Database.PostgreSql.IntegrationTests
{
    /// <summary>
    ///     Behaviour that belongs to PostgreSQL and Npgsql, or that the shared suite cannot reach:
    ///     jsonb, timestamp handling, identity retrieval, the COPY writer and identifier quoting.
    /// </summary>
    [Collection(PostgreSqlGroup.Name)]
    public class PostgreSqlSpecificTests(PostgreSqlFixture fixture) : IAsyncLifetime
    {
        private static readonly Type[] Tables =
        [
            typeof(PgJsonRow), typeof(PgMomentRow), typeof(PgShortRow), typeof(PgIntKeyRow),
            typeof(PgLongKeyRow), typeof(PgGuidKeyRow), typeof(PgCopyRow), typeof(PgOddRow)
        ];

        public async ValueTask InitializeAsync()
        {
            await using var connection = await fixture.OpenAsync();
            await DropAll(connection);
            foreach (var table in Tables.Where(t => t != typeof(PgOddRow)))
                await connection.CreateTable(table, CancellationToken.None);
        }

        public async ValueTask DisposeAsync()
        {
            await using var connection = await fixture.OpenAsync();
            await DropAll(connection);
            GC.SuppressFinalize(this);
        }

        private static async Task DropAll(DbConnection connection)
        {
            foreach (var table in Tables)
                await connection.DropTableIfExists(table, CancellationToken.None);
            await connection.Execute("DROP SCHEMA IF EXISTS pgother CASCADE");
        }

        private static async Task<object?> Scalar(DbConnection connection, string sql, DbTransaction? transaction = null)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Transaction = transaction;
            return await command.ExecuteScalarAsync();
        }

        private static async Task<long> Count(DbConnection connection, string table, DbTransaction? transaction = null)
            => (long)(await Scalar(connection, $"SELECT count(*) FROM \"{table}\"", transaction))!;

        private static async Task<string?> ColumnType(DbConnection connection, string table, string column)
            => (string?)await Scalar(connection, $"SELECT data_type FROM information_schema.columns WHERE table_name = '{table}' AND column_name = '{column}' AND table_schema = current_schema()");

        private static List<PgShortRow> ShortRows(int count, string prefix = "id")
            => Enumerable.Range(0, count).Select(i => new PgShortRow { Id = $"{prefix}{i}", Code = "abc" }).ToList();

        #region jsonb

        [Fact]
        public async Task Json_is_a_jsonb_column_unless_a_length_makes_it_a_varchar()
        {
            await using var connection = await fixture.OpenAsync();

            Assert.Equal("jsonb", await ColumnType(connection, "PgJson", "Details"));
            Assert.Equal("character varying", await ColumnType(connection, "PgJson", "Capped"));
        }

        [Fact]
        public async Task A_json_value_is_stored_as_a_jsonb_object_and_round_trips()
        {
            await using var connection = await fixture.OpenAsync();
            var id = await connection.NewKey<PgJsonRow>();

            await connection.Insert(new PgJsonRow
            {
                Id = id,
                Details = new PgDocument { Kind = "k", Values = [3, 1, 2] },
                Capped = new PgDocument { Kind = "c", Values = [7] }
            });

            Assert.Equal("object", await Scalar(connection, $"SELECT jsonb_typeof(\"Details\") FROM \"PgJson\" WHERE \"Id\" = '{id}'"));
            var read = await connection.QueryKey<PgJsonRow>(id);
            Assert.Equal([3, 1, 2], read!.Details!.Values);
            Assert.Equal("c", read.Capped!.Kind);
        }

        [Fact]
        public async Task A_json_value_can_be_updated()
        {
            await using var connection = await fixture.OpenAsync();
            var row = new PgJsonRow { Id = await connection.NewKey<PgJsonRow>(), Details = new PgDocument { Kind = "a" } };
            await connection.Insert(row);

            row.Details = new PgDocument { Kind = "b", Values = [9] };
            await connection.Update(row);

            var read = await connection.QueryKey<PgJsonRow>(row.Id);
            Assert.Equal("b", read!.Details!.Kind);
            Assert.Equal([9], read.Details.Values);
        }

        [Fact]
        public async Task A_null_json_value_reads_back_as_null_whichever_way_it_was_written()
        {
            //Pinned as the core behaves: Insert writes the text 'null' (a jsonb null), COPY writes a
            //SQL NULL. Both read back as null.
            await using var connection = await fixture.OpenAsync();
            var inserted = await connection.NewKey<PgJsonRow>();
            var copied = await connection.NewKey<PgJsonRow>();

            await connection.Insert(new PgJsonRow { Id = inserted, Details = null });
            await connection.BulkInsert([new PgJsonRow { Id = copied, Details = null }]);

            Assert.Equal("null", await Scalar(connection, $"SELECT jsonb_typeof(\"Details\") FROM \"PgJson\" WHERE \"Id\" = '{inserted}'"));
            Assert.Equal(1L, await Scalar(connection, $"SELECT count(*) FROM \"PgJson\" WHERE \"Id\" = '{copied}' AND \"Details\" IS NULL"));
            Assert.Null((await connection.QueryKey<PgJsonRow>(inserted))!.Details);
            Assert.Null((await connection.QueryKey<PgJsonRow>(copied))!.Details);
        }

        [Fact]
        public async Task A_json_value_goes_through_copy()
        {
            await using var connection = await fixture.OpenAsync();
            var row = new PgJsonRow
            {
                Id = await connection.NewKey<PgJsonRow>(),
                Details = new PgDocument { Kind = "bulk", Values = [1, 2] },
                Capped = new PgDocument { Kind = "capped" }
            };

            await connection.BulkInsert([row]);

            Assert.Equal("object", await Scalar(connection, $"SELECT jsonb_typeof(\"Details\") FROM \"PgJson\" WHERE \"Id\" = '{row.Id}'"));
            var read = await connection.QueryKey<PgJsonRow>(row.Id);
            Assert.Equal([1, 2], read!.Details!.Values);
            Assert.Equal("capped", read.Capped!.Kind);
        }

        #endregion jsonb

        #region timestamps

        [Fact]
        public async Task A_DateTimeOffset_with_an_offset_is_stored_as_the_same_instant()
        {
            await using var connection = await fixture.OpenAsync();
            var moment = new DateTimeOffset(2026, 11, 24, 13, 0, 0, TimeSpan.FromHours(5));
            var row = new PgMomentRow { Id = await connection.NewKey<PgMomentRow>(), Moment = moment };

            await connection.Insert(row);
            Assert.Equal(moment, (await connection.QueryKey<PgMomentRow>(row.Id))!.Moment);

            var later = moment.AddHours(2);
            row.Moment = later;
            await connection.Update(row);
            Assert.Equal(later, (await connection.QueryKey<PgMomentRow>(row.Id))!.Moment);

            Assert.Equal(row.Id, Assert.Single(await connection.Query<PgMomentRow>(new { Moment = later })).Id);
        }

        [Fact]
        public async Task A_DateTimeOffset_with_an_offset_goes_through_copy()
        {
            await using var connection = await fixture.OpenAsync();
            var moment = new DateTimeOffset(2026, 11, 24, 13, 0, 0, TimeSpan.FromHours(-8));
            var row = new PgMomentRow { Id = await connection.NewKey<PgMomentRow>(), Moment = moment };

            await connection.BulkInsert([row]);

            Assert.Equal(moment, (await connection.QueryKey<PgMomentRow>(row.Id))!.Moment);
        }

        [Theory]
        [InlineData(DateTimeKind.Utc)]
        [InlineData(DateTimeKind.Local)]
        [InlineData(DateTimeKind.Unspecified)]
        public async Task A_DateTime_of_any_kind_is_stored_as_written_and_read_back_unspecified(DateTimeKind kind)
        {
            //timestamp holds no zone, and Npgsql refuses a Utc value for it: the provider strips the
            //kind and keeps the ticks, whichever of insert, update or COPY wrote them.
            await using var connection = await fixture.OpenAsync();
            var at = new DateTime(2026, 11, 24, 13, 14, 15, 123, kind);
            var row = new PgMomentRow { Id = await connection.NewKey<PgMomentRow>(), Stamp = at };

            await connection.Insert(row);
            var read = await connection.QueryKey<PgMomentRow>(row.Id);
            Assert.Equal(DateTimeKind.Unspecified, read!.Stamp.Kind);
            Assert.Equal(at.Ticks, read.Stamp.Ticks);

            var later = at.AddMinutes(5);
            row.Stamp = later;
            await connection.Update(row);
            Assert.Equal(later.Ticks, (await connection.QueryKey<PgMomentRow>(row.Id))!.Stamp.Ticks);

            var bulk = new PgMomentRow { Id = await connection.NewKey<PgMomentRow>(), Stamp = at };
            await connection.BulkInsert([bulk]);
            Assert.Equal(at.Ticks, (await connection.QueryKey<PgMomentRow>(bulk.Id))!.Stamp.Ticks);
        }

        #endregion timestamps

        #region identity and keys

        [Fact]
        public async Task Insert_returns_an_int_for_an_int_identity()
        {
            await using var connection = await fixture.OpenAsync();

            var first = await connection.Insert(new PgIntKeyRow { Label = "a" }, CancellationToken.None);
            var second = await connection.Insert(new PgIntKeyRow { Label = "b" }, CancellationToken.None);

            Assert.Equal(1, Assert.IsType<int>(first));
            Assert.Equal(2, Assert.IsType<int>(second));
        }

        [Fact]
        public async Task Insert_returns_a_long_for_a_bigint_identity_past_the_int_range()
        {
            await using var connection = await fixture.OpenAsync();
            await connection.Execute("ALTER TABLE \"PgLongKey\" ALTER COLUMN \"Id\" RESTART WITH 3000000000");

            var id = await connection.Insert(new PgLongKeyRow { Label = "big" }, CancellationToken.None);

            Assert.Equal(3_000_000_000L, Assert.IsType<long>(id));
            Assert.Equal("big", (await connection.QueryKey<PgLongKeyRow>(3_000_000_000L))!.Label);
        }

        [Fact]
        public async Task An_identity_is_BY_DEFAULT_so_a_row_can_be_seeded_with_its_own_key()
        {
            await using var connection = await fixture.OpenAsync();

            await connection.Execute("INSERT INTO \"PgIntKey\" (\"Id\", \"Label\") VALUES (500, 'seeded')");
            var generated = await connection.Insert<PgIntKeyRow, int>(new PgIntKeyRow { Label = "next" });

            Assert.Equal("seeded", (await connection.QueryKey<PgIntKeyRow>(500))!.Label);
            Assert.NotEqual(500, generated);
        }

        [Fact]
        public async Task A_Guid_key_is_a_uuid_the_server_generates()
        {
            await using var connection = await fixture.OpenAsync();
            Assert.Equal("uuid", await ColumnType(connection, "PgGuidKey", "Id"));

            await connection.Insert(new PgGuidKeyRow { Label = "one" });
            await connection.Insert(new PgGuidKeyRow { Label = "two" });

            var rows = await connection.Query<PgGuidKeyRow>();
            Assert.Equal(2, rows.Count);
            Assert.All(rows, r => Assert.NotEqual(Guid.Empty, r.Id));
            Assert.NotEqual(rows[0].Id, rows[1].Id);
        }

        [Fact]
        public async Task Four_and_a_half_thousand_int_keys_are_read_in_batches()
        {
            //Above the 2,000 a statement is allowed to carry, so the read splits into three.
            await using var connection = await fixture.OpenAsync();
            await connection.Execute("INSERT INTO \"PgIntKey\" (\"Label\") SELECT 'x' FROM generate_series(1, 4500)");
            var keys = (await connection.Query<PgIntKeyRow>()).Select(r => (object)r.Id).ToList();

            var rows = await connection.QueryKeys<PgIntKeyRow>(keys);

            Assert.Equal(4500, keys.Count);
            Assert.Equal(4500, rows.Count);
        }

        [Fact]
        public async Task Four_and_a_half_thousand_uuid_keys_are_read_in_batches()
        {
            await using var connection = await fixture.OpenAsync();
            await connection.Execute("INSERT INTO \"PgGuidKey\" (\"Label\") SELECT 'x' FROM generate_series(1, 4500)");
            var keys = (await connection.Query<PgGuidKeyRow>()).Select(r => (object)r.Id).ToList();

            var rows = await connection.QueryKeys<PgGuidKeyRow>(keys);

            Assert.Equal(4500, keys.Count);
            Assert.Equal(4500, rows.Count);
        }

        #endregion identity and keys

        #region COPY

        [Fact]
        public async Task A_bulk_insert_of_ten_thousand_rows_arrives_complete()
        {
            await using var connection = await fixture.OpenAsync();

            await connection.BulkInsert(ShortRows(10_000));

            Assert.Equal(10_000L, await Count(connection, "PgShort"));
        }

        [Fact]
        public async Task A_duplicate_key_inside_the_batch_writes_nothing()
        {
            await using var connection = await fixture.OpenAsync();
            var rows = ShortRows(50);
            rows.Add(new PgShortRow { Id = "id3", Code = "dup" });

            var failure = await Assert.ThrowsAsync<PostgresException>(() => connection.BulkInsert(rows));

            Assert.Equal("23505", failure.SqlState);
            Assert.Equal(0L, await Count(connection, "PgShort"));
        }

        [Fact]
        public async Task A_duplicate_of_an_existing_row_writes_nothing_of_the_batch()
        {
            await using var connection = await fixture.OpenAsync();
            await connection.Insert(new PgShortRow { Id = "existing", Code = "old" });
            var rows = ShortRows(20);
            rows.Insert(10, new PgShortRow { Id = "existing", Code = "new" });

            await Assert.ThrowsAsync<PostgresException>(() => connection.BulkInsert(rows));

            Assert.Equal(1L, await Count(connection, "PgShort"));
            Assert.Equal("old", (await connection.QueryKey<PgShortRow>("existing"))!.Code);
        }

        [Fact]
        public async Task A_value_too_long_in_the_last_row_writes_nothing_of_the_batch()
        {
            await using var connection = await fixture.OpenAsync();
            var rows = ShortRows(1000);
            rows.Add(new PgShortRow { Id = "last", Code = "123456" });

            var failure = await Assert.ThrowsAsync<PostgresException>(() => connection.BulkInsert(rows));

            Assert.Equal("22001", failure.SqlState);
            Assert.Equal(0L, await Count(connection, "PgShort"));
        }

        [Fact]
        public async Task A_failed_copy_leaves_the_connection_usable()
        {
            await using var connection = await fixture.OpenAsync();
            var rows = ShortRows(5);
            rows.Add(new PgShortRow { Id = "id1", Code = "dup" });

            await Assert.ThrowsAsync<PostgresException>(() => connection.BulkInsert(rows));
            await connection.BulkInsert(ShortRows(5, "again"));

            Assert.Equal(5L, await Count(connection, "PgShort"));
        }

        [Fact]
        public async Task A_copy_in_a_transaction_is_rolled_back_with_it()
        {
            await using var connection = await fixture.OpenAsync();
            await using (var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken))
            {
                await connection.BulkInsert(ShortRows(100), CancellationToken.None, transaction);
                Assert.Equal(100L, await Count(connection, "PgShort", transaction));

                await transaction.RollbackAsync(TestContext.Current.CancellationToken);
            }

            Assert.Equal(0L, await Count(connection, "PgShort"));
        }

        [Fact]
        public async Task A_copy_in_a_transaction_is_kept_when_it_commits()
        {
            await using var connection = await fixture.OpenAsync();
            await using (var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken))
            {
                await connection.BulkInsert(ShortRows(100), CancellationToken.None, transaction);
                //Invisible to another connection until the commit.
                await using var other = await fixture.OpenAsync();
                Assert.Equal(0L, await Count(other, "PgShort"));

                await transaction.CommitAsync(TestContext.Current.CancellationToken);
            }

            Assert.Equal(100L, await Count(connection, "PgShort"));
        }

        [Fact]
        public async Task A_copy_that_fails_inside_a_transaction_leaves_the_earlier_work_of_the_transaction_alone()
        {
            await using var connection = await fixture.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);
            await connection.Insert(new PgShortRow { Id = "before", Code = "ok" }, CancellationToken.None, transaction);
            await connection.Execute("SAVEPOINT s", null, CancellationToken.None, transaction);

            var rows = ShortRows(5);
            rows.Add(new PgShortRow { Id = "id2", Code = "dup" });
            await Assert.ThrowsAsync<PostgresException>(() => connection.BulkInsert(rows, CancellationToken.None, transaction));
            await connection.Execute("ROLLBACK TO SAVEPOINT s", null, CancellationToken.None, transaction);

            Assert.Equal(1L, await Count(connection, "PgShort", transaction));
            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        }

        [Fact]
        public async Task A_cancelled_token_writes_nothing_and_leaves_the_connection_usable()
        {
            await using var connection = await fixture.OpenAsync();
            using var cancelled = new CancellationTokenSource();
            await cancelled.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => connection.BulkInsert(ShortRows(100), cancelled.Token));

            Assert.Equal(0L, await Count(connection, "PgShort"));
            await connection.BulkInsert(ShortRows(3));
            Assert.Equal(3L, await Count(connection, "PgShort"));
        }

        [Fact]
        public async Task An_empty_bulk_insert_does_not_reach_the_server()
        {
            //PgOddRow has no table: any statement sent for it would fail.
            await using var connection = await fixture.OpenAsync();

            await connection.BulkInsert(new List<PgOddRow>());
        }

        [Fact]
        public async Task A_transaction_of_another_connection_is_refused()
        {
            await using var connection = await fixture.OpenAsync();
            await using var other = await fixture.OpenAsync();
            await using var foreign = await other.BeginTransactionAsync(TestContext.Current.CancellationToken);

            await Assert.ThrowsAsync<DatabaseException>(() => connection.BulkInsert(ShortRows(2), CancellationToken.None, foreign));

            Assert.Equal(0L, await Count(connection, "PgShort"));
        }

        [Fact]
        public async Task Every_column_kind_survives_copy_with_and_without_a_declared_type()
        {
            await using var connection = await fixture.OpenAsync();
            var row = new PgCopyRow
            {
                Id = await connection.NewKey<PgCopyRow>(),
                Label = "all",
                Details = new PgDocument { Kind = "d", Values = [1] },
                Stamp = new DateTime(2026, 11, 24, 13, 14, 15, 123, DateTimeKind.Utc),
                Moment = new DateTimeOffset(2026, 11, 24, 13, 14, 15, TimeSpan.FromHours(5)),
                DeclaredStamp = new DateTime(2026, 11, 24, 1, 2, 3, DateTimeKind.Local),
                DeclaredMoment = new DateTimeOffset(2026, 11, 24, 1, 2, 3, TimeSpan.FromHours(-3)),
                DeclaredJson = new PgDocument { Kind = "j", Values = [2] },
                DeclaredPlainJson = new PgDocument { Kind = "p", Values = [3] },
                Capped = new PgDocument { Kind = "c" }
            };

            await connection.BulkInsert([row]);

            var read = await connection.QueryKey<PgCopyRow>(row.Id);
            Assert.Equal("all", read!.Label);
            Assert.Equal([1], read.Details!.Values);
            Assert.Equal(row.Stamp.Ticks, read.Stamp.Ticks);
            Assert.Equal(row.Moment, read.Moment);
            Assert.Equal(row.DeclaredStamp.Ticks, read.DeclaredStamp.Ticks);
            Assert.Equal(row.DeclaredMoment, read.DeclaredMoment);
            Assert.Equal([2], read.DeclaredJson!.Values);
            Assert.Equal([3], read.DeclaredPlainJson!.Values);
            Assert.Equal("c", read.Capped!.Kind);
        }

        [Fact]
        public async Task A_utc_DateTime_in_a_column_with_a_declared_type_goes_through_copy()
        {
            //Written without a type, a Utc DateTime would be taken for a timestamptz.
            await using var connection = await fixture.OpenAsync();
            var row = new PgCopyRow
            {
                Id = await connection.NewKey<PgCopyRow>(),
                DeclaredStamp = new DateTime(2026, 11, 24, 1, 2, 3, DateTimeKind.Utc)
            };

            await connection.BulkInsert([row]);

            Assert.Equal(row.DeclaredStamp.Ticks, (await connection.QueryKey<PgCopyRow>(row.Id))!.DeclaredStamp.Ticks);
        }

        #endregion COPY

        #region identifiers, comparisons and errors

        [Fact]
        public async Task A_name_left_unquoted_in_raw_sql_is_folded_to_lower_case_and_not_found()
        {
            //The DDL quotes every identifier, so the catalog holds "PgShort"; raw SQL written for SQL
            //Server or MySQL has to be rewritten with double quotes.
            await using var connection = await fixture.OpenAsync();

            var failure = await Assert.ThrowsAsync<PostgresException>(() => connection.Execute("SELECT * FROM PgShort"));

            Assert.Equal("42P01", failure.SqlState);
            await connection.Execute("SELECT * FROM \"PgShort\"");
        }

        [Fact]
        public async Task A_reserved_word_and_a_double_quote_in_a_name_are_quoted()
        {
            await using var connection = await fixture.OpenAsync();
            await connection.CreateTable<PgOddRow>();
            var row = new PgOddRow { Id = await connection.NewKey<PgOddRow>(), Select = 7, MixedCase = "x" };

            await connection.Insert(row);
            await connection.BulkInsert([new PgOddRow { Id = await connection.NewKey<PgOddRow>(), Select = 8 }]);
            row.Select = 9;
            await connection.Update(row);

            Assert.True(await connection.TableExists<PgOddRow>());
            Assert.Equal(2, (await connection.Query<PgOddRow>()).Count);
            Assert.Equal(9, (await connection.QueryKey<PgOddRow>(row.Id))!.Select);
            Assert.Equal(row.Id, Assert.Single(await connection.Query<PgOddRow>(new { @select = 9 })).Id);
        }

        [Fact]
        public async Task A_table_in_another_schema_is_created_found_and_dropped()
        {
            await using var connection = await fixture.OpenAsync();
            await connection.Execute("CREATE SCHEMA pgother");
            Assert.False(await connection.TableExists<PgSchemedRow>());

            await connection.CreateTable<PgSchemedRow>();
            Assert.True(await connection.TableExists<PgSchemedRow>());
            await connection.Insert(new PgSchemedRow { Id = await connection.NewKey<PgSchemedRow>() });
            Assert.Single(await connection.Query<PgSchemedRow>());

            await connection.DropTable<PgSchemedRow>();
            Assert.False(await connection.TableExists<PgSchemedRow>());
        }

        [Fact]
        public async Task Text_comparisons_are_case_and_accent_sensitive()
        {
            await using var connection = await fixture.OpenAsync();
            await connection.Insert(new PgShortRow { Id = "a", Code = "x", Free = "Éclair" });

            Assert.Single(await connection.Query<PgShortRow>(new { Free = "Éclair" }));
            Assert.Empty(await connection.Query<PgShortRow>(new { Free = "éclair" }));
            Assert.Empty(await connection.Query<PgShortRow>(new { Free = "Eclair" }));
        }

        [Fact]
        public async Task A_string_without_a_length_is_text()
        {
            await using var connection = await fixture.OpenAsync();

            Assert.Equal("text", await ColumnType(connection, "PgShort", "Free"));
            Assert.Equal("character varying", await ColumnType(connection, "PgShort", "Code"));
        }

        [Fact]
        public async Task A_value_longer_than_the_varchar_is_an_error_and_not_a_truncation()
        {
            await using var connection = await fixture.OpenAsync();

            var failure = await Assert.ThrowsAsync<PostgresException>(() => connection.Insert(new PgShortRow { Id = "a", Code = "123456" }));

            Assert.Equal("22001", failure.SqlState);
            Assert.Equal(0L, await Count(connection, "PgShort"));
        }

        #endregion identifiers, comparisons and errors
    }
}
