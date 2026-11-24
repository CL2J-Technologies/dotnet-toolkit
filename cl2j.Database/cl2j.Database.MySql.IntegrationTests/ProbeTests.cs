using System.Data;
using System.Data.Common;
using System.Text;
using cl2j.Database.DataAnnotations;
using MySqlConnector;
using Xunit;

namespace cl2j.Database.MySql.IntegrationTests
{
    [Table("My_Probe")]
    public sealed class MyProbeRow
    {
        [Column(Name = "Id", Key = KeyType.Key, Length = 40)]
        public string Id { get; set; } = string.Empty;

        [Column(Name = "Moment")]
        public DateTimeOffset Moment { get; set; }

        [Column(Name = "At")]
        public DateTime At { get; set; }
    }

    [Collection(MySqlTestGroup.Name)]
    public class ProbeTests(MySqlFixture fixture)
    {
        private static async Task<string> Raw(DbConnection connection, string sql)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            var value = await command.ExecuteScalarAsync();
            return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "<null>";
        }

        [Fact]
        public async Task Probe()
        {
            var sb = new StringBuilder("\nEVIDENCE\n");
            await using var connection = await fixture.OpenAsync();
            await connection.DropTableIfExists(typeof(MyProbeRow), CancellationToken.None);
            await connection.CreateTable<MyProbeRow>();
            var moment = new DateTimeOffset(2026, 11, 24, 8, 0, 0, TimeSpan.FromHours(-5));
            var at = new DateTime(2026, 11, 24, 8, 0, 0, DateTimeKind.Unspecified).AddTicks(1234567);

            async Task Step(string name, Func<Task<string>> body)
            {
                try { sb.AppendLine($"{name}: {await body()}"); }
                catch (Exception e) { sb.AppendLine($"{name}: EXCEPTION {e.GetType().Name} {e.Message}"); }
            }

            await Step("insert param (-05:00 08:00)", async () =>
            {
                await connection.Insert(new MyProbeRow { Id = "ins", Moment = moment, At = at });
                return await Raw(connection, "SELECT CONCAT(CAST(`Moment` AS CHAR),' | ',CAST(`At` AS CHAR)) FROM `My_Probe` WHERE `Id`='ins'");
            });

            await Step("update param", async () =>
            {
                await connection.Update(new MyProbeRow { Id = "ins", Moment = moment.AddHours(1), At = at.AddTicks(2) });
                return await Raw(connection, "SELECT CONCAT(CAST(`Moment` AS CHAR),' | ',CAST(`At` AS CHAR)) FROM `My_Probe` WHERE `Id`='ins'");
            });

            await Step("batched bulk", async () =>
            {
                await connection.BulkInsert([new MyProbeRow { Id = "bat", Moment = moment, At = at }]);
                return await Raw(connection, "SELECT CONCAT(CAST(`Moment` AS CHAR),' | ',CAST(`At` AS CHAR)) FROM `My_Probe` WHERE `Id`='bat'");
            });

            await Step("bulk copy", async () =>
            {
                await using var c2 = await fixture.BulkCopy.OpenAsync();
                await c2.BulkInsert([new MyProbeRow { Id = "cop", Moment = moment, At = at }]);
                return await Raw(c2, "SELECT CONCAT(CAST(`Moment` AS CHAR),' | ',CAST(`At` AS CHAR)) FROM `My_Probe` WHERE `Id`='cop'");
            });

            foreach (var (label, value) in new (string, object)[]
            {
                ("raw DateTimeOffset -05:00", moment),
                ("raw DateTime Utc .1234567", DateTime.SpecifyKind(at, DateTimeKind.Utc)),
                ("raw DateTime Local .1234567", DateTime.SpecifyKind(at, DateTimeKind.Local)),
                ("raw DateTime Unspecified .1234567", at),
                ("raw DateTime Unspecified .1234565", at.AddTicks(-2)),
            })
            {
                foreach (var dbType in new[] { (DbType?)null, DbType.DateTime2, DbType.DateTime })
                {
                    await Step($"{label} DbType={dbType?.ToString() ?? "none"}", async () =>
                    {
                        await using var command = connection.CreateCommand();
                        command.CommandText = "SELECT CAST(CAST(@p AS DATETIME(6)) AS CHAR)";
                        var p = command.CreateParameter();
                        p.ParameterName = "p";
                        p.Value = value;
                        if (dbType is { } t)
                            p.DbType = t;
                        command.Parameters.Add(p);
                        return Convert.ToString(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture) ?? "<null>";
                    });
                }
            }

            foreach (var dbType in new[] { DbType.AnsiString, DbType.String })
            {
                await Step($"string 'Renée' DbType={dbType}", async () =>
                {
                    await using var command = connection.CreateCommand();
                    command.CommandText = "SELECT CONCAT(@p,' | ',HEX(@p),' | ',CHARSET(@p))";
                    var p = command.CreateParameter();
                    p.ParameterName = "p";
                    p.Value = "Renée Ω";
                    p.DbType = dbType;
                    command.Parameters.Add(p);
                    return Convert.ToString(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture) ?? "<null>";
                });
            }

            await Step("max_allowed_packet / sql_mode / collation / tz", async () =>
                await Raw(connection, "SELECT CONCAT(@@max_allowed_packet,' | ',@@sql_mode,' | ',@@collation_database,' | ',@@time_zone,' | ',VERSION())"));

            await connection.DropTableIfExists(typeof(MyProbeRow), CancellationToken.None);
            await connection.DropTableIfExists(typeof(MyShortRow), CancellationToken.None);
            await connection.CreateTable<MyShortRow>();
            await Step("too long: Insert", async () => { await connection.Insert(new MyShortRow { Id = "a", Short = "toolong" }); return "no error, stored=" + await Raw(connection, "SELECT `Short` FROM `My_Short` WHERE `Id`='a'"); });
            await Step("too long: batched BulkInsert", async () => { await connection.BulkInsert([new MyShortRow { Id = "b", Short = "toolong" }]); return "no error, stored=" + await Raw(connection, "SELECT `Short` FROM `My_Short` WHERE `Id`='b'"); });
            await Step("too long: bulk copy BulkInsert", async () => { await using var c2 = await fixture.BulkCopy.OpenAsync(); await c2.BulkInsert([new MyShortRow { Id = "c", Short = "toolong" }]); return "no error, stored=" + await Raw(c2, "SELECT `Short` FROM `My_Short` WHERE `Id`='c'"); });
            await Step("raw MySqlBulkCopy too long + dup", async () =>
            {
                await using var c2 = (MySqlConnection)await fixture.BulkCopy.OpenAsync();
                var dt = new DataTable();
                dt.Columns.Add("Id", typeof(string));
                dt.Columns.Add("Short", typeof(string));
                dt.Rows.Add("d", "toolong");
                dt.Rows.Add("a", "dup");
                var bc = new MySqlBulkCopy(c2) { DestinationTableName = "`My_Short`" };
                bc.ColumnMappings.Add(new MySqlBulkCopyColumnMapping(0, "Id"));
                bc.ColumnMappings.Add(new MySqlBulkCopyColumnMapping(1, "Short"));
                var r = await bc.WriteToServerAsync(dt);
                return $"rowsCopied={r.RowsInserted} warnings=[{string.Join("; ", r.Warnings.Select(w => $"{w.Level}/{w.Code}/{w.Message}"))}]";
            });
            await connection.DropTableIfExists(typeof(MyShortRow), CancellationToken.None);
            Assert.Fail(sb.ToString());
        }
    }
}

namespace cl2j.Database.MySql.IntegrationTests
{
    [Table("My_Short")]
    public sealed class MyShortRow
    {
        [Column(Name = "Id", Key = KeyType.Key, Length = 10)]
        public string Id { get; set; } = string.Empty;

        [Column(Name = "Short", Length = 5)]
        public string Short { get; set; } = string.Empty;
    }
}
