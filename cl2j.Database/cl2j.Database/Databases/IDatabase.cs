
using System.Data.Common;

namespace cl2j.Database.Databases
{
    public interface IDatabase
    {
        Task<DbConnection> CreateConnection();

        Task<int> Execute(string sql, object? param = null);

        Task<string> NewKey<T>();
        Task<T> Insert<T>(T t) where T : class;
        Task<T> Update<T>(T t) where T : class;
        Task DeleteKey<T>(object key) where T : class;

        Task<string> BuildSelectStatement<T>(string? where = null);

        Task<List<T>> Query<T>(string sql, object? param = null);
        Task<List<T>> Query<T>(object? param);

        Task<T?> QuerySingle<T>(string sql, object? param = null);
        Task<T?> QuerySingle<T>(object param);

        Task<bool> Exists<T>(object param);
    }
}