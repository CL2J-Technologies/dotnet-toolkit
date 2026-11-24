using System.Text;
using Xunit;

namespace cl2j.Database.IntegrationTests
{
    [Collection(SqlServerCollection.Name)]
    public class TmpProbe(SqlServerFixture fixture)
    {
        [Fact]
        public async Task Probe()
        {
            await using var connection = await fixture.OpenAsync();
            await connection.Execute("IF OBJECT_ID('TmpDec') IS NOT NULL DROP TABLE TmpDec; CREATE TABLE TmpDec (V decimal(38,2))");
            var sb = new StringBuilder();
            for (var k = 1; k <= 24; k++)
            {
                var value = decimal.Parse(new string('9', k) + ".99", System.Globalization.CultureInfo.InvariantCulture);
                try
                {
                    await connection.Execute("INSERT INTO TmpDec (V) VALUES (@V)", new { V = value });
                    sb.Append($"{k}:ok ");
                }
                catch (Exception ex)
                {
                    sb.Append($"{k}:{ex.GetType().Name} ");
                }
            }
            await connection.Execute("DROP TABLE TmpDec");
            Assert.Fail(sb.ToString());
        }
    }
}
