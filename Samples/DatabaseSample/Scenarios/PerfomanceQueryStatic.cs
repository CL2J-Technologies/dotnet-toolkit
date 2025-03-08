using System.Data;
using BenchmarkDotNet.Attributes;
using cl2j.Database;
using cl2j.Database.SqlServer;
using Dapper;
using DatabaseSample.Models;
using Microsoft.Data.SqlClient;

namespace DatabaseSample.Scenarios
{
    public class PerfomanceQueryStatic
    {
        public static string ConnectionString = "Data Source=.;Initial Catalog=DatabaseSample;Integrated Security=True;MultipleActiveResultSets=False;Connection Timeout=15;encrypt=true;trustServerCertificate=true";
        public static string SelectClients = "SELECT [Id], [Name], [Balance], [Active], [CreatedOn] FROM [Client]";
        private SqlConnection Connection = null!;

        [GlobalSetup]
        public void GlobalSetup()
        {
            SqlServer.Register();

            Connection = new SqlConnection(ConnectionString);
            Connection.Open();
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            Connection?.Dispose();
        }

        [Benchmark]
        public async Task QueryClientCl2j()
        {
            _ = await Connection.Query<Client>(SelectClients);
        }

        //[Benchmark]
        public async Task QueryClientStaticCast()
        {
            var cmd = Connection.CreateCommand();
            cmd.CommandText = SelectClients;
            cmd.CommandType = CommandType.Text;

            using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleResult | CommandBehavior.SequentialAccess);
            if (reader.HasRows)
            {
                var list = new List<Client>();
                while (await reader.ReadAsync())
                {
                    var client = new Client
                    {
                        Id = (int)reader[0],
                        Name = (string)reader[1],
                        Balance = (decimal)reader[2],
                        Active = (bool)reader[3],
                        CreatedOn = (DateTimeOffset)reader[4]
                    };
                    list.Add(client);
                }
            }
        }

        [Benchmark]
        public async Task QueryClientStaticGetMethod()
        {
            var cmd = Connection.CreateCommand();
            cmd.CommandText = SelectClients;
            cmd.CommandType = CommandType.Text;

            using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleResult | CommandBehavior.SequentialAccess);
            if (reader.HasRows)
            {
                var list = new List<Client>();
                while (await reader.ReadAsync())
                {
                    var client = new Client
                    {
                        Id = reader.GetInt32(0),
                        Name = reader.GetString(1),
                        Balance = reader.GetDecimal(2),
                        Active = reader.GetBoolean(3),
                        CreatedOn = reader.GetDateTimeOffset(4)
                    };
                    list.Add(client);
                }
            }
        }

        //[Benchmark]
        public async Task QueryClientStaticGetValue()
        {
            var cmd = Connection.CreateCommand();
            cmd.CommandText = SelectClients;
            cmd.CommandType = CommandType.Text;
            using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleResult | CommandBehavior.SequentialAccess);
            if (reader.HasRows)
            {
                var list = new List<Client>();
                while (await reader.ReadAsync())
                {
                    var client = new Client
                    {
                        Id = (int)reader.GetValue(0),
                        Name = (string)reader.GetValue(1),
                        Balance = (decimal)reader.GetValue(2),
                        Active = (bool)reader.GetValue(3),
                        CreatedOn = (DateTimeOffset)reader.GetValue(4)
                    };
                    list.Add(client);
                }
            }
        }

        [Benchmark]
        public async Task QueryClientDapper()
        {
            _ = await Connection.QueryAsync<Client>(SelectClients);
        }
    }
}
