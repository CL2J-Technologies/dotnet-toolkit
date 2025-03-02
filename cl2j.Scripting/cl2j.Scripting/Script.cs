using System.Reflection;

namespace cl2j.Scripting
{
    public class Script(object instance, ScriptOptions options)
    {
        public void Execute()
        {
            instance.GetType().InvokeMember(options.MethodName, BindingFlags.InvokeMethod, null, instance, []);
        }

        public TOut? Execute<TOut>()
        {
            var result = instance.GetType().InvokeMember(options.MethodName, BindingFlags.InvokeMethod, null, instance, []);
            return result is null ? default : (TOut)result;
        }

        public TOut? Execute<TIn, TOut>(TIn? context)
        {
            var parameters = context is null ? [] : new List<object> { context };
            var result = instance.GetType().InvokeMember(options.MethodName, BindingFlags.InvokeMethod, null, instance, [.. parameters]);
            return result is null ? default : (TOut)result;
        }

        public static Script Create(ScriptOptions options)
        {
            var instance = options.InstanceCreator.CreateInstance(options);
            return new Script(instance, options);
        }
    }
}
