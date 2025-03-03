using System.Data.Common;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace cl2j.Database.Databases
{
    public abstract class BaseDatabase(DatabaseOptions options, ILogger logger)
    {
        protected readonly ILogger logger = logger;

        protected abstract Task<DbConnection> CreateConnection();

        public async Task<List<T>> Query<T>(string sql, object? param = null)
        {
            try
            {
                var sw = Stopwatch.StartNew();
                using var connection = await CreateConnection();
                var list = (await connection.Query<T>(sql, param)).ToList();
                logger.Log(options.TraceLevel, $"GetAll<{typeof(T).Name}>({param}) -> {list.Count} [{sw.ElapsedMilliseconds}ms]");

                return list;
            }
            catch (Exception ex)
            {
                logger.Log(options.ExceptionLevel, ex, $"Database.GetAll<{typeof(T).Name}> - Exception thrown\n\tsql={sql}\n\tparameters={param}\n\tException={ex.Message}");
                throw;
            }
        }

        public async Task<T?> QuerySingle<T>(string sql, object? param = null)
        {
            try
            {
                using var connection = await CreateConnection();
                var t = await connection.QuerySingle<T>(sql, param);
                return t;
            }
            catch (Exception ex)
            {
                logger.Log(options.ExceptionLevel, ex, $"Database.Get<{typeof(T).Name}> - Exception thrown\n\tsql={sql}\n\tparameters={param}\n\tException={ex.Message}");
                throw;
            }
        }

        public async Task Insert<T>(T t) where T : class
        {
            try
            {
                using var connection = await CreateConnection();
                await connection.Insert(t);
            }
            catch (Exception ex)
            {
                logger.Log(options.ExceptionLevel, ex, $"Database.Insert<{typeof(T).Name}> - Exception thrown");
                throw;
            }
        }

        public async Task Update<T>(T t) where T : class
        {
            try
            {
                using var connection = await CreateConnection();
                await connection.Update(t);
            }
            catch (Exception ex)
            {
                logger.Log(options.ExceptionLevel, ex, $"Database.Update<{typeof(T).Name}> - Exception thrown");
                throw;
            }
        }

        public async Task<int> Execute(string sql, object? param = null)
        {
            try
            {
                using var connection = await CreateConnection();
                return await connection.Execute(sql, param);
            }
            catch (Exception ex)
            {
                logger.Log(options.ExceptionLevel, ex, $"Database.Execute - Exception thrown\n\tsql={sql}\n\tparameters={param}\n\tException={ex.Message}");
                throw;
            }
        }
    }
}
