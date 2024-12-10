using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;

namespace cl2j.SqlServerMaintenancePlan.IndexMaintenance.Repository
{
    public class SqlServerIndexRepository(string connectionString)
    {
        public async Task<List<IndexFragmentation>> GetIndexesFragmentation()
        {
            const string sql = @"
                SELECT sch.name as [Schema], objs.name as [Table], i.name AS [Index], ips.avg_fragmentation_in_percent as [fragmentation]
                FROM sys.dm_db_partition_stats ps
			    INNER JOIN sys.objects objs on ps.object_id = objs.object_id
			    INNER JOIN sys.schemas sch ON objs.schema_id = sch.schema_id
			    INNER JOIN sys.indexes i ON ps.object_id = i.object_id AND ps.index_id = i.index_id
			    INNER JOIN sys.objects obj on ps.object_id = obj.object_id
			    CROSS APPLY sys.dm_db_index_physical_stats(DB_ID(), ps.object_id, ps.index_id, null, 'LIMITED') ips";

            using var connection = await CreateConnection();
            var list = (await connection.QueryAsync<IndexFragmentation>(sql, commandTimeout: 3600)).ToList();
            return list;
        }

        public async Task RebuildOnlineAsync(string schema, string table, string index)
        {
            await Execute($"ALTER INDEX [{index}] ON [{schema}].[{table}] REBUILD WITH (ONLINE=ON)");
        }

        public async Task ReorganizeAsync(string schema, string table, string index)
        {
            await Execute($"ALTER INDEX [{index}] ON [{schema}].[{table}] REORGANIZE");
        }

        #region Private 

        private async Task<IDbConnection> CreateConnection()
        {
            var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            return connection;
        }

        private async Task Execute(string sql)
        {
            using var connection = await CreateConnection();
            await connection.ExecuteAsync(sql, commandTimeout: 3600);
        }

        #endregion Private
    }
}