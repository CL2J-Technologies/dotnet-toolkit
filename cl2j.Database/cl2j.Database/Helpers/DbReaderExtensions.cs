using System.Collections.Concurrent;
using System.Data.Common;
using System.Text;
using cl2j.Database.Descriptors;
using cl2j.Database.Exceptions;
using cl2j.Scripting;
using cl2j.Tooling;

namespace cl2j.Database.Helpers
{
    public static class DbReaderExtensions
    {
        private static readonly ConcurrentDictionary<Type, Script> CacheRead = [];
        private static readonly ConcurrentDictionary<Type, Script> CacheReadSingle = [];

        public static async Task<List<T>> Read<T>(this DbDataReader reader, TableDescriptor tableDescriptor)
        {
            if (!reader.HasRows)
                return [];

            try
            {
                var type = typeof(T);
                if (!type.IsVisible)
                    throw new DatabaseException($"Type {type.Name} must be public");

                var script = CacheRead.GetOrAdd(type, type =>
                {
                    var code = GenerateReadCode<T>(tableDescriptor);
#if DEBUG
                    try
                    {
#endif
                        var options = ScriptOptions.Create<DbDataReader, List<T>>(code);
                        ConfigureScriptOptions<T>(options, tableDescriptor);
                        return Script.Create(options);
#if DEBUG
                    }
#pragma warning disable CS0168 // Variable is declared but never used
                    catch (Exception ex)
#pragma warning restore CS0168 // Variable is declared but never used
                    {
                        throw;
                    }
#endif
                });
                return script.Execute<DbDataReader, List<T>>(reader) ?? [];
            }
            finally
            {
                await reader.CloseAsync();
            }
        }

        public static async Task<T?> ReadSingle<T>(this DbDataReader reader, TableDescriptor tableDescriptor)
        {
            if (!reader.HasRows)
                return default;

            try
            {
                var type = typeof(T);
                var script = CacheReadSingle.GetOrAdd(type, type =>
                {
                    var code = GenerateReadSingleCode<T>(tableDescriptor);
                    var options = ScriptOptions.Create<DbDataReader, T>(code);
                    ConfigureScriptOptions<T>(options, tableDescriptor);
                    return Script.Create(options);
                });
                return script.Execute<DbDataReader, T>(reader);
            }
            finally
            {
                await reader.CloseAsync();
            }
        }

        private static string GenerateReadCode<T>(TableDescriptor tableDescriptor)
        {
            var typeName = TypeUtils.GetTypeName<T>();

            var sb = new StringBuilder();
            sb.AppendLine($"List<{typeName}> list = [];");
            sb.AppendLine("while (context.Read())");
            sb.AppendLine("{");
            AddReadObjectCode<T>(sb, typeName!, tableDescriptor);
            sb.AppendLine();
            sb.AppendLine("\tlist.Add(t);");
            sb.AppendLine("}");
            sb.AppendLine("return list;");
            return sb.ToString();
        }

        private static string GenerateReadSingleCode<T>(TableDescriptor tableDescriptor)
        {
            var typeName = TypeUtils.GetTypeName<T>();

            var sb = new StringBuilder();
            sb.AppendLine("context.Read();");
            AddReadObjectCode<T>(sb, typeName!, tableDescriptor);
            sb.AppendLine("return t;");
            return sb.ToString();
        }

        private static void AddReadObjectCode<T>(StringBuilder sb, string typeName, TableDescriptor tableDescriptor)
        {
            for (var i = 0; i < tableDescriptor.Columns.Count; ++i)
            {
                var c = tableDescriptor.Columns[i];
                var propType = c.Property.PropertyType;
                var propSetter = $"t.{c.Name}";
                var propTypeName = TypeUtils.GetTypeName(propType);

                var vi = $"v{i}";

                sb.Append($"\tvar {vi}=");
                if (c.ColumnAtribute.Json)
                    sb.AppendLine($"JsonSerializer.Deserialize<{propTypeName}>(context.GetString({i}));");
                else if (c.ColumnAtribute.Key == DataAnnotations.KeyType.Key || c.ColumnAtribute.Required)
                {
                    if (propType == Types.TypeBool)
                        sb.AppendLine($"context.GetBoolean({i});");
                    else if (propType == Types.TypeShort)
                        sb.AppendLine($"context.GetInt16({i});");
                    else if (propType == Types.TypeInt)
                        sb.AppendLine($"context.GetInt32({i});");
                    else if (propType == Types.TypeLong)
                        sb.AppendLine($"context.GetInt64({i});");
                    else if (propType == Types.TypeDecimal)
                        sb.AppendLine($"context.GetDecimal({i});");
                    else if (propType == Types.TypeFloat)
                        sb.AppendLine($"context.GetFloat({i});");
                    else if (propType == Types.TypeDouble)
                        sb.AppendLine($"context.GetDouble({i});");
                    else if (propType == Types.TypeGuid)
                        sb.AppendLine($"context.GetGuid({i});");
                    else if (propType == Types.TypeString)
                        sb.AppendLine($"context.GetString({i});");
                    else if (propType == Types.TypeDateTime)
                        sb.AppendLine($"context.GetDateTime({i});");
                    else if (propType.IsEnum)
                        sb.AppendLine($"({propTypeName})context.GetInt32({i});");
                    else
                        sb.AppendLine($"({propTypeName})context.GetValue({i});");
                }
                else
                {
                    if (propType.IsEnum)
                        sb.AppendLine($"({propTypeName})context.GetInt32({i});");
                    else
                        sb.AppendLine($"context.GetValue({i});");
                }
            }
            sb.AppendLine();
            sb.AppendLine($"\tvar t = new {typeName} {{");
            for (var i = 0; i < tableDescriptor.Columns.Count; ++i)
            {
                var c = tableDescriptor.Columns[i];
                var propTypeName = TypeUtils.GetTypeName(c.Property.PropertyType);
                var vi = $"v{i}";
                if (c.ColumnAtribute.Key == DataAnnotations.KeyType.Key || c.ColumnAtribute.Required || c.ColumnAtribute.Json || c.Property.PropertyType.IsEnum)
                    sb.Append($"\t\t{c.Name}={vi}");
                else
                    sb.Append($"\t\t{c.Name}=({vi} == DBNull.Value) ? default : ({propTypeName}){vi}");
                if (i < tableDescriptor.Columns.Count - 1) sb.Append(",");
                sb.AppendLine();
            }
            sb.AppendLine("\t};");
        }

        private static void ConfigureScriptOptions<T>(ScriptOptions options, TableDescriptor tableDescriptor)
        {
            var type = typeof(T);

            options.AddNamespaces([
                "System",
                "System.Data",
                "System.Data.Common",
                "System.Collections.Generic",
                "cl2j.Database.Helpers",
            ]);
            if (type.Namespace is not null)
                options.AddNamespaces(type.Namespace);

            if (tableDescriptor.Columns.Any(c => c.ColumnAtribute.Json))
            {
                options.AddNamespaces("System.Text.Json");
                options.AddAssembly(typeof(System.Text.Json.JsonSerializer));
            }
        }
    }
}
