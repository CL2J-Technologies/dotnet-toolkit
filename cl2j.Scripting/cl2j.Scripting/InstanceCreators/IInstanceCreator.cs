namespace cl2j.Scripting.InstanceCreators
{
    public interface IInstanceCreator
    {
        object CreateInstance(ScriptOptions options);
    }
}