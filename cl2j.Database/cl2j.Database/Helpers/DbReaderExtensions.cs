using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
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

            var type = typeof(T);
            Script? script = null;

            try
            {
                if (!type.IsVisible)
                    throw new DatabaseException($"Type {type.Name} must be public");

                script = CacheRead.GetOrAdd(type, type =>
                {
                    var code = GenerateReadCode<T>(tableDescriptor);
#if DEBUG
                    try
                    {
#endif
                        return CreateScript<List<T>>(code, tableDescriptor);
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

                //Script.Execute lets what the generated reader throws through as itself, so a
                //missing column or a value that would not convert surfaces with its own message.
                return script.Execute<DbDataReader, List<T>>(reader) ?? [];
            }
            catch
            {
                Debug.WriteLine($"DbReaderExtensions.Read<{type.Name}> : GENERATED CODE");
                if (script is not null)
                    Debug.WriteLine(script.Options.Code);
                throw;
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
                    return CreateScript<T>(code, tableDescriptor);
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
            //Resolved once, before the loop: the ordinals are a property of the result set, not of
            //the row.
            AddOrdinalLookupCode(sb, tableDescriptor);
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
            AddOrdinalLookupCode(sb, tableDescriptor);
            sb.AppendLine("context.Read();");
            AddReadObjectCode<T>(sb, typeName!, tableDescriptor);
            sb.AppendLine("return t;");
            return sb.ToString();
        }

        /// <summary>
        ///     Emits one <c>GetOrdinal</c> per column, so the generated reader addresses the result
        ///     set by name.
        ///
        ///     <para>
        ///     It used to address it by position, with the descriptor index written straight into
        ///     <c>context.GetString(3)</c> and the like. That only works while the SELECT lists
        ///     columns in exactly the order the type declares its properties. Raw SQL is a
        ///     supported entry point and <c>SELECT *</c> follows the physical order of the table,
        ///     which nothing keeps aligned with the type and an <c>ALTER TABLE</c> can change. When
        ///     two columns of the same shape swapped, the values landed in each other's properties
        ///     with no exception and no warning. See issue #25.
        ///     </para>
        ///
        ///     <para>
        ///     <c>GetOrdinal</c> throws when the name is absent, which turns a silent misalignment
        ///     into a failure that names the column it wanted.
        ///     </para>
        /// </summary>
        private static void AddOrdinalLookupCode(StringBuilder sb, TableDescriptor tableDescriptor)
        {
            for (var i = 0; i < tableDescriptor.Columns.Count; ++i)
            {
                var name = tableDescriptor.Columns[i].Name.Replace("\"", "\\\"", StringComparison.Ordinal);
                sb.AppendLine($"var o{i}=context.GetOrdinal(\"{name}\");");
            }
        }

        private static void AddReadObjectCode<T>(StringBuilder sb, string typeName, TableDescriptor tableDescriptor)
        {
            for (var i = 0; i < tableDescriptor.Columns.Count; ++i)
            {
                var c = tableDescriptor.Columns[i];
                var propType = c.Property.PropertyType;
                var propTypeName = TypeUtils.GetTypeName(propType);

                var vi = $"v{i}";
                var ordinal = $"o{i}";

                sb.Append($"\tvar {vi}=");
                if (c.ColumnAtribute.Json)
                    //SQL NULL reads as null. A bulk insert writes NULL for a null property, where Insert
                    //writes the text 'null'; GetString on the NULL used to throw.
                    sb.AppendLine($"context.IsDBNull({ordinal}) ? default({propTypeName}) : JsonSerializer.Deserialize<{propTypeName}>(context.GetString({ordinal}));");
                else if (c.ColumnAtribute.Key == DataAnnotations.KeyType.Key || c.ColumnAtribute.Required)
                {
                    if (propType == Types.TypeBool)
                        sb.AppendLine($"context.GetBoolean({ordinal});");
                    else if (propType == Types.TypeShort)
                        sb.AppendLine($"context.GetInt16({ordinal});");
                    else if (propType == Types.TypeInt)
                        sb.AppendLine($"context.GetInt32({ordinal});");
                    else if (propType == Types.TypeLong)
                        sb.AppendLine($"context.GetInt64({ordinal});");
                    else if (propType == Types.TypeDecimal)
                        sb.AppendLine($"context.GetDecimal({ordinal});");
                    else if (propType == Types.TypeFloat)
                        sb.AppendLine($"context.GetFloat({ordinal});");
                    else if (propType == Types.TypeDouble)
                        sb.AppendLine($"context.GetDouble({ordinal});");
                    else if (propType == Types.TypeGuid)
                        sb.AppendLine($"context.GetGuid({ordinal});");
                    else if (propType == Types.TypeString)
                        sb.AppendLine($"context.GetString({ordinal});");
                    else if (propType == Types.TypeDateTime)
                        sb.AppendLine($"context.GetDateTime({ordinal});");
                    else if (propType.IsEnum)
                        sb.AppendLine($"({propTypeName})context.GetInt32({ordinal});");
                    else
                        sb.AppendLine($"({propTypeName})context.GetValue({ordinal});");
                }
                else
                {
                    if (propType.IsEnum)
                        sb.AppendLine($"({propTypeName})context.GetInt32({ordinal});");
                    else if (IsNullableEnum(propType))
                    {
                        //Read as the enum, not through GetValue: the provider hands back a boxed
                        //int, and unboxing it straight to E? throws InvalidCastException.
                        var enumTypeName = TypeUtils.GetTypeName(Nullable.GetUnderlyingType(propType)!);
                        sb.AppendLine($"context.IsDBNull({ordinal}) ? ({propTypeName})null : ({enumTypeName})context.GetInt32({ordinal});");
                    }
                    else
                        sb.AppendLine($"context.GetValue({ordinal});");
                }
            }
            sb.AppendLine();
            sb.AppendLine($"\tvar t = new {typeName} {{");
            for (var i = 0; i < tableDescriptor.Columns.Count; ++i)
            {
                var c = tableDescriptor.Columns[i];
                var propTypeName = TypeUtils.GetTypeName(c.Property.PropertyType);
                var vi = $"v{i}";
                if (c.ColumnAtribute.Key == DataAnnotations.KeyType.Key || c.ColumnAtribute.Required || c.ColumnAtribute.Json || c.Property.PropertyType.IsEnum || IsNullableEnum(c.Property.PropertyType))
                    sb.Append($"\t\t{c.Name}={vi}");
                else
                    sb.Append($"\t\t{c.Name}=({vi} == DBNull.Value) ? default : ({propTypeName}){vi}");
                if (i < tableDescriptor.Columns.Count - 1) sb.Append(',');
                sb.AppendLine();
            }
            sb.AppendLine("\t};");
        }

        private static bool IsNullableEnum(Type type) => Nullable.GetUnderlyingType(type)?.IsEnum == true;

        private static Script CreateScript<T>(string code, TableDescriptor tableDescriptor)
        {
            var options = ScriptOptions.Create<DbDataReader, T>(code, addDefault: true);
            ConfigureScriptOptions<T>(options, tableDescriptor);
            var script = Script.Create(options);
            return script;
        }

        private static void ConfigureScriptOptions<T>(ScriptOptions options, TableDescriptor tableDescriptor)
        {
            var type = typeof(T);

            options.AddNamespaces(
                "System",
                "System.Data",
                "System.Data.Common",
                "System.Collections.Generic",
                "cl2j.Database.Helpers"
            );
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
