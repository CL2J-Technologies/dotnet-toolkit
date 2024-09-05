using System.Reflection;
using cl2j.Scripting.InstanceResolvers;

namespace cl2j.Scripting
{
    public class ScriptEngine(ScriptDeclaration scriptDeclaration, IInstanceCreator instanceCreator) : IScriptEngine
    {
        private readonly string MethodName = "Execute";

        private static ScriptDeclaration defaultScriptDeclaration = ScriptDeclaration.CreateDefault();
        private static IInstanceCreator defaultInstanceCreator = InstanceCreatorCache.Create();

        public static void SetDefaultInstanceCreator(IInstanceCreator instanceCreator)
        {
            defaultInstanceCreator = instanceCreator;
        }

        public static void SetDefaultScriptDeclaration(ScriptDeclaration scriptDeclaration)
        {
            defaultScriptDeclaration = scriptDeclaration;
        }

        public static ScriptEngine Create(ScriptDeclaration? scriptDeclaration = null)
        {
            return new ScriptEngine(
                scriptDeclaration ?? defaultScriptDeclaration,
                defaultInstanceCreator);
        }

        public static string AddReturn(string code)
        {
            return $"return {code};";
        }

        public ScriptExecutionResult Execute(string code)
        {
            var methodDeclaration = $"public void {MethodName}()";
            return ExecuteMethod<object?, object?>(scriptDeclaration, instanceCreator, code, null, methodDeclaration); ;
        }

        public ScriptExecutionResult<TOut> Execute<TOut>(string code)
        {
            var outType = GetTypeName<TOut>();
            var methodDeclaration = $"public {outType} {MethodName}()";

            return ExecuteMethod<object?, TOut>(scriptDeclaration, instanceCreator, code, null, methodDeclaration); ;
        }

        public ScriptExecutionResult<TOut> Execute<TIn, TOut>(string code, TIn context)
        {
            var inType = GetTypeName<TIn>();
            var outType = GetTypeName<TOut>();
            var methodDeclaration = $"public {outType} {MethodName}({inType} context)";

            return ExecuteMethod<TIn, TOut>(scriptDeclaration, instanceCreator, code, context, methodDeclaration);
        }

        #region Private Methods

        private ScriptExecutionResult<TOut> ExecuteMethod<TIn, TOut>(ScriptDeclaration scriptDeclaration, IInstanceCreator instanceCreator, string code, TIn context, string methodDeclaration)
        {
            object? instance;
            try
            {
                instance = instanceCreator.CreateInstance(scriptDeclaration, methodDeclaration, code);
            }
            catch (Exception ex)
            {
                return new ScriptExecutionResult<TOut> { ErrorType = ScriptErrorTypes.Compilation, Exception = ex };
            }

            try
            {
                var result = InvokeMethod<object?, TOut>(instance!, MethodName, context);
                return new ScriptExecutionResult<TOut> { Result = result };
            }
            catch (Exception ex)
            {
                return new ScriptExecutionResult<TOut> { ErrorType = ScriptErrorTypes.Execution, Exception = ex };
            }
        }

        private TOut? InvokeMethod<TIn, TOut>(object instance, string method, TIn? context)
        {
            ArgumentNullException.ThrowIfNull(instance);

            var parameters = context is null ? [] : new List<object> { context };

            var result = instance.GetType().InvokeMember(method, BindingFlags.InvokeMethod, null, instance, [.. parameters]);
            return result is null ? default : (TOut)result;
        }

        private static string? GetTypeName<T>()
        {
            return typeof(T).FullName;
        }

        #endregion //Private Methods
    }
}
