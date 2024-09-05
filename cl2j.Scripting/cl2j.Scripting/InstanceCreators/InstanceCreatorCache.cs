using System.Collections.Concurrent;

namespace cl2j.Scripting.InstanceResolvers
{
    public class InstanceCreatorCache(IInstanceCreator instanceCreator) : IInstanceCreator
    {
        private static readonly ConcurrentDictionary<int, object> instances = new();

        public object CreateInstance(ScriptDeclaration scriptDeclaration, string methodDeclaration, string code)
        {
            var hashcode = $"{methodDeclaration}-{code}".GetHashCode();
            var instance = instances.GetOrAdd(hashcode, x =>
            {
                return instanceCreator.CreateInstance(scriptDeclaration, methodDeclaration, code);
            });
            return instance;
        }

        public static IInstanceCreator Create()
        {
            return new InstanceCreatorCache(new InstanceCreator());
        }
    }
}
