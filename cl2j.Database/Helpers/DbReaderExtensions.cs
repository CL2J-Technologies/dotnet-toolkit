using System.Collections.Concurrent;
using System.Data.Common;
using System.Text;
using cl2j.Database.Descriptors;
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
                var script = CacheRead.GetOrAdd(type, type =>
                {
                    var code = GenerateReadCode<T>(tableDescriptor);
                    var options = ScriptOptions.Create<DbDataReader, List<T>>(code);
                    ConfigureScriptOptions<T>(options, tableDescriptor);
                    return Script.Create(options);
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
            var typeName = typeof(T).FullName;

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
            var typeName = typeof(T).FullName;

            var sb = new StringBuilder();
            sb.AppendLine("context.Read();");
            AddReadObjectCode<T>(sb, typeName!, tableDescriptor);
            sb.AppendLine("return t;");
            return sb.ToString();
        }

        private static void AddReadObjectCode<T>(StringBuilder sb, string typeName, TableDescriptor tableDescriptor)
        {
            sb.AppendLine($"\tvar t = new {typeName}();");
            sb.AppendLine();
            for (var i = 0; i < tableDescriptor.Columns.Count; ++i)
            {
                var c = tableDescriptor.Columns[i];
                var propType = c.Property.PropertyType;
                var propSetter = $"t.{c.Name}";

                if (c.ColumnAtribute.Required)
                {
                    if (c.ColumnAtribute.Json)
                    {
                        var vi = $"v{i}";
                        var propTypeName = TypeUtils.GetTypeName(propType);

                        sb.AppendLine($"\tvar {vi}=context.GetString({i});");
                        sb.AppendLine($"\t{propSetter}=JsonSerializer.Deserialize<{propTypeName}>({vi});");
                    }
                    else if (propType == Types.TypeBool)
                        sb.AppendLine($"\t{propSetter}=context.GetBoolean({i});");
                    else if (propType == Types.TypeShort)
                        sb.AppendLine($"\t{propSetter}=context.GetInt16({i});");
                    else if (propType == Types.TypeInt)
                        sb.AppendLine($"\t{propSetter}=context.GetInt32({i});");
                    else if (propType == Types.TypeLong)
                        sb.AppendLine($"\t{propSetter}=context.GetInt64({i});");
                    else if (propType == Types.TypeDecimal)
                        sb.AppendLine($"\t{propSetter}=context.GetDecimal({i});");
                    else if (propType == Types.TypeFloat)
                        sb.AppendLine($"\t{propSetter}=context.GetFloat({i});");
                    else if (propType == Types.TypeDouble)
                        sb.AppendLine($"\t {propSetter}=context.GetDouble({i});");
                    else if (propType == Types.TypeGuid)
                        sb.AppendLine($"\t {propSetter}=context.GetGuid({i});");
                    else if (propType == Types.TypeString)
                        sb.AppendLine($"\t {propSetter}=context.GetString({i});");
                    else if (propType == Types.TypeDateTime)
                        sb.AppendLine($"\t {propSetter}=context.GetDateTime({i});");
                    else
                        sb.AppendLine($"\t{propSetter}=({propType.Name})context.GetValue({i});");
                }
                else
                {
                    var vi = $"v{i}";
                    sb.AppendLine($"\tvar {vi} = context.GetValue({i});");
                    sb.Append($"\tif({vi} != DBNull.Value) ");
                    if (c.ColumnAtribute.Json)
                    {
                        var propTypeName = TypeUtils.GetTypeName(propType);
                        sb.AppendLine($"{propSetter}=JsonSerializer.Deserialize<{propTypeName}>((string){vi});");
                    }
                    else
                        sb.AppendLine($"{propSetter}=({propType.Name}){vi};");
                }
            }
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

            options.AddAssembly(typeof(Script));
            options.AddAssembly(typeof(DbDataReader));
            options.AddAssembly(typeof(decimal));
            options.AddAssembly(typeof(T));

            options.AddAssemblies([
                "System.Runtime.dll",
                "System.Private.CoreLib.dll",
                "System.Data.dll",
                "System.Collections.dll",
                "cl2j.Database.dll"
            ]);

            if (tableDescriptor.Columns.Any(c => c.ColumnAtribute.Json))
            {
                options.AddNamespaces("System.Text.Json");
                options.AddAssembly(typeof(System.Text.Json.JsonSerializer));
            }
        }
    }
}
