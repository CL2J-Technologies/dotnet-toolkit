using System.Text;
using Xunit;

namespace cl2j.Database.MySql.IntegrationTests
{
    [Collection(MySqlTestGroup.Name)]
    public class ProbeTests(MySqlFixture fixture)
    {
        [Fact]
        public async Task Probe()
        {
            var sb = new StringBuilder("\nEVIDENCE\n");

            foreach (var (label, strategy) in new[] { ("insert", 0), ("batched", 1), ("bulkcopy", 2) })
            {
                foreach (var (what, at, day, clock) in new[]
                {
                    ("year 1", DateTime.MinValue, DateOnly.MinValue, TimeOnly.MinValue),
                    ("999-12-31", new DateTime(999, 12, 31, 23, 59, 59), new DateOnly(999, 12, 31), TimeOnly.MaxValue),
                    ("max", DateTime.MaxValue, DateOnly.MaxValue, TimeOnly.MaxValue),
                }.Select(x => (x.Item1, x.Item2, x.Item3, x.Item4)))
                {
                    await using var connection = strategy == 2 ? await fixture.BulkCopy.OpenAsync() : await fixture.OpenAsync();
                    await connection.DropTableIfExists(typeof(MyTemporalRow), CancellationToken.None);
                    await connection.CreateTable<MyTemporalRow>();
                    try
                    {
                        var row = new MyTemporalRow { Id = "r", Moment = new DateTimeOffset(at, TimeSpan.Zero), At = at, Day = day, Clock = clock };
                        if (strategy == 0)
                            await connection.Insert(row);
                        else
                            await connection.BulkInsert([row]);
                        await using var command = connection.CreateCommand();
                        command.CommandText = "SELECT CONCAT(CAST(`At` AS CHAR),' | ',CAST(`Moment` AS CHAR),' | ',CAST(`Day` AS CHAR),' | ',CAST(`Clock` AS CHAR)) FROM `My_Temporal`";
                        sb.AppendLine($"{label} {what}: {await command.ExecuteScalarAsync()}");
                    }
                    catch (Exception e)
                    {
                        sb.AppendLine($"{label} {what}: EXCEPTION {e.GetType().Name} {e.Message}");
                    }
                    await connection.DropTableIfExists(typeof(MyTemporalRow), CancellationToken.None);
                }
            }

            Assert.Fail(sb.ToString());
        }
    }
}
