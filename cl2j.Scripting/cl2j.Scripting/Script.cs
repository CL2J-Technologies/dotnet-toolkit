using System.Reflection;

namespace cl2j.Scripting
{
    public class Script(object instance, ScriptOptions options)
    {
        // DoNotWrapExceptions: what the script throws reaches the caller as itself, not wrapped in a
        // TargetInvocationException that hides the message behind "Exception has been thrown by the
        // target of an invocation". The compiled delegate below behaves the same way, so the path a
        // call takes never changes what it throws.
        private const BindingFlags InvokeFlags = BindingFlags.InvokeMethod | BindingFlags.DoNotWrapExceptions;

        private readonly Type instanceType = instance.GetType();
        private readonly string methodName = options.MethodName;

        // The delegate bound for the last (TIn, TOut) pair Execute<TIn, TOut> was called with, or
        // NoDelegate when that pair cannot be bound directly. A single slot: a script is compiled for
        // one signature, so one pair is the case that matters. A race between threads only builds the
        // same delegate twice — a reference write is atomic, and either result is correct.
        private object? boundExecute;

        private static readonly object NoDelegate = new();

        public ScriptOptions Options => options;

        public void Execute()
        {
            instanceType.InvokeMember(methodName, InvokeFlags, null, instance, []);
        }

        public TOut? Execute<TOut>()
        {
            var result = instanceType.InvokeMember(methodName, InvokeFlags, null, instance, []);
            return result is null ? default : (TOut)result;
        }

        public TOut? Execute<TIn, TOut>(TIn? context)
        {
            if (context is not null)
            {
                // The hot path: cl2j.Database calls this once per query. Through InvokeMember each
                // call paid for a member lookup and an argument array — about 210 ns and 370 bytes.
                if (boundExecute is not Func<TIn, TOut> execute)
                {
                    var bound = ReferenceEquals(boundExecute, NoDelegate) ? null : Bind<TIn, TOut>();
                    if (bound is null)
                        return Invoke<TOut>([context]);
                    execute = bound;
                }

                return execute(context);
            }

            // A null context passes no argument at all, so it resolves to a parameterless overload.
            return Invoke<TOut>([]);
        }

        public static Script Create(ScriptOptions options)
        {
            var instance = options.InstanceCreator.CreateInstance(options);
            return new Script(instance, options);
        }

        private TOut? Invoke<TOut>(object[] parameters)
        {
            var result = instanceType.InvokeMember(methodName, InvokeFlags, null, instance, parameters);
            return result is null ? default : (TOut)result;
        }

        private Func<TIn, TOut>? Bind<TIn, TOut>()
        {
            // Only an exact match is bound: a method whose parameter or return type merely converts
            // (an int returned as object, an overload taking a base type) keeps going through
            // InvokeMember, which already handles it.
            var method = instanceType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static, [typeof(TIn)]);

            Func<TIn, TOut>? execute = null;
            if (method is not null && method.ReturnType == typeof(TOut))
            {
                execute = method.IsStatic
                    ? (Func<TIn, TOut>?)Delegate.CreateDelegate(typeof(Func<TIn, TOut>), method, throwOnBindFailure: false)
                    : (Func<TIn, TOut>?)Delegate.CreateDelegate(typeof(Func<TIn, TOut>), instance, method, throwOnBindFailure: false);
            }

            boundExecute = (object?)execute ?? NoDelegate;
            return execute;
        }
    }
}
