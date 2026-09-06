using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.ExceptionServices;
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

                return script.Execute<DbDataReader, List<T>>(reader) ?? [];
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                Debug.WriteLine($"DbReaderExtensions.Read<{type.Name}> : GENERATED CODE");
                if (script is not null)
                    Debug.WriteLine(script.Options.Code);

                //The generated reader runs through reflection, so everything it throws arrives
                //wrapped in "Exception has been thrown by the target of an invocation" — which says
                //nothing about the column that was missing or the value that would not convert.
                //Rethrow the real one, keeping its stack.
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
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

                try
                {
                    return script.Execute<DbDataReader, T>(reader);
                }
                catch (TargetInvocationException ex) when (ex.InnerException is not null)
                {
                    //Same unwrapping as Read: the reflection wrapper hides the only part of the
                    //message a caller can act on.
                    ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                    throw;
                }
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
                    sb.AppendLine($"JsonSerializer.Deserialize<{propTypeName}>(context.GetString({ordinal}));");
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
                if (c.ColumnAtribute.Key == DataAnnotations.KeyType.Key || c.ColumnAtribute.Required || c.ColumnAtribute.Json || c.Property.PropertyType.IsEnum)
                    sb.Append($"\t\t{c.Name}={vi}");
                else
                    sb.Append($"\t\t{c.Name}=({vi} == DBNull.Value) ? default : ({propTypeName}){vi}");
                if (i < tableDescriptor.Columns.Count - 1) sb.Append(',');
                sb.AppendLine();
            }
            sb.AppendLine("\t};");
        }

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
