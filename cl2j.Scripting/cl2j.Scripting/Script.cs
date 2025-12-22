using System.Reflection;

namespace cl2j.Scripting
{
    public class Script(object instance, ScriptOptions options)
    {
        private readonly Type instanceType = instance.GetType();
        private readonly string methodName = options.MethodName;

        public ScriptOptions Options => options;

        public void Execute()
        {
            instanceType.InvokeMember(methodName, BindingFlags.InvokeMethod, null, instance, []);
        }

        public TOut? Execute<TOut>()
        {
            var result = instanceType.InvokeMember(methodName, BindingFlags.InvokeMethod, null, instance, []);
            return result is null ? default : (TOut)result;
        }

        public TOut? Execute<TIn, TOut>(TIn? context)
        {
            var parameters = context is null ? [] : new List<object> { context };
            var result = instanceType.InvokeMember(methodName, BindingFlags.InvokeMethod, null, instance, [.. parameters]);
            return result is null ? default : (TOut)result;
        }

        public static Script Create(ScriptOptions options)
        {
            var instance = options.InstanceCreator.CreateInstance(options);
            return new Script(instance, options);
        }
    }
}
