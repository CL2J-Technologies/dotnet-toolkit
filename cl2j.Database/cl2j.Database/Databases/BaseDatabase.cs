using System.Data.Common;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace cl2j.Database.Databases
{
    public abstract class BaseDatabase(DatabaseOptions options, ILogger logger) : IDatabase
    {
        protected readonly ILogger logger = logger;

        public abstract Task<DbConnection> CreateConnection();

        public async Task<string> BuildSelectStatement<T>(string? sqlStatement = null)
        {
            using var connection = await CreateConnection();
            return connection.BuildSelectStatement<T>(sqlStatement);
        }

        public async Task<List<T>> Query<T>()
        {
            try
            {
                using var connection = await CreateConnection();
                var result = await connection.Query<T>();
                return result;
            }
            catch (Exception ex)
            {
                if (logger.IsEnabled(options.ExceptionLevel))
                    logger.Log(options.ExceptionLevel, ex, $"Database.Get<{typeof(T).Name}> - Exception thrown\n\tException={ex.Message}");
                throw;
            }
        }

        public async Task<List<T>> Query<T>(string sql, object? param = null)
        {
            try
            {
                var sw = Stopwatch.StartNew();
                using var connection = await CreateConnection();
                var list = (await connection.Query<T>(sql, param)).ToList();
                if (logger.IsEnabled(options.TraceLevel))
                    logger.Log(options.TraceLevel, $"GetAll<{typeof(T).Name}>({param}) -> {list.Count} [{sw.ElapsedMilliseconds}ms]");

                return list;
            }
            catch (Exception ex)
            {
                if (logger.IsEnabled(options.ExceptionLevel))
                    logger.Log(options.ExceptionLevel, ex, $"Database.GetAll<{typeof(T).Name}> - Exception thrown\n\tsql={sql}\n\tparameters={param}\n\tException={ex.Message}");
                throw;
            }
        }

        public async Task<List<T>> Query<T>(object? param)
        {
            try
            {
                using var connection = await CreateConnection();
                var result = await connection.Query<T>(param);
                return result;
            }
            catch (Exception ex)
            {
                if (logger.IsEnabled(options.ExceptionLevel))
                    logger.Log(options.ExceptionLevel, ex, $"Database.Get<{typeof(T).Name}> - Exception thrown\n\tparameters={param}\n\tException={ex.Message}");
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
                if (logger.IsEnabled(options.ExceptionLevel))
                    logger.Log(options.ExceptionLevel, ex, $"Database.Get<{typeof(T).Name}> - Exception thrown\n\tsql={sql}\n\tparameters={param}\n\tException={ex.Message}");
                throw;
            }
        }

        public async Task<T?> QuerySingle<T>(object param)
        {
            try
            {
                using var connection = await CreateConnection();
                var t = await connection.QuerySingle<T>(param);
                return t;
            }
            catch (Exception ex)
            {
                if (logger.IsEnabled(options.ExceptionLevel))
                    logger.Log(options.ExceptionLevel, ex, $"Database.Get<{typeof(T).Name}> - Exception thrown\n\tparameters={param}\n\tException={ex.Message}");
                throw;
            }
        }

        public async Task<List<T>> QueryKeys<T>(IEnumerable<string> keys)
        {
            try
            {
                using var connection = await CreateConnection();
                var result = await connection.QueryKeys<T>(keys);
                return result;
            }
            catch (Exception ex)
            {
                if (logger.IsEnabled(options.ExceptionLevel))
                    logger.Log(options.ExceptionLevel, ex, $"Database.QueryKeys<{typeof(T).Name}> - Exception thrown\n\tException={ex.Message}");
                throw;
            }
        }

        public async Task<bool> Exists<T>(object param)
        {
            var t = await QuerySingle<T>(param);
            return t is not null;
        }


        public async Task<string> NewKey<T>()
        {
            try
            {
                using var connection = await CreateConnection();
                return await connection.NewKey<T>();
            }
            catch (Exception ex)
            {
                if (logger.IsEnabled(options.ExceptionLevel))
                    logger.Log(options.ExceptionLevel, ex, $"Database.Insert<{typeof(T).Name}> - Exception thrown");
                throw;
            }
        }


        public async Task<T> Insert<T>(T t) where T : class
        {
            try
            {
                using var connection = await CreateConnection();
                await connection.Insert(t);
                return t;
            }
            catch (Exception ex)
            {
                if (logger.IsEnabled(options.ExceptionLevel))
                    logger.Log(options.ExceptionLevel, ex, $"Database.Insert<{typeof(T).Name}> - Exception thrown");
                throw;
            }
        }

        public async Task<T> Update<T>(T t) where T : class
        {
            try
            {
                using var connection = await CreateConnection();
                await connection.Update(t);
                return t;
            }
            catch (Exception ex)
            {
                if (logger.IsEnabled(options.ExceptionLevel))
                    logger.Log(options.ExceptionLevel, ex, $"Database.Update<{typeof(T).Name}> - Exception thrown");
                throw;
            }
        }

        public async Task Update<T, TValue>(T t, string columnName, TValue value) where T : class
        {
            try
            {
                using var connection = await CreateConnection();
                await connection.UpdateColumn(t, columnName, value);
            }
            catch (Exception ex)
            {
                if (logger.IsEnabled(options.ExceptionLevel))
                    logger.Log(options.ExceptionLevel, ex, $"Database.Update<{typeof(T).Name}> - Exception thrown");
                throw;
            }
        }

        public async Task DeleteKey<T>(object key) where T : class
        {
            try
            {
                using var connection = await CreateConnection();
                await connection.DeleteKey<T>(key);
            }
            catch (Exception ex)
            {
                if (logger.IsEnabled(options.ExceptionLevel))
                    logger.Log(options.ExceptionLevel, ex, $"Database.DeleteKey<{typeof(T).Name}> - Exception thrown");
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
                if (logger.IsEnabled(options.ExceptionLevel))
                    logger.Log(options.ExceptionLevel, ex, $"Database.Execute - Exception thrown\n\tsql={sql}\n\tparameters={param}\n\tException={ex.Message}");
                throw;
            }
        }
    }
}
