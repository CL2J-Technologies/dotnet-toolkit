namespace cl2j.Scripting.InstanceResolvers
{
    public interface IInstanceCreator
    {
        object CreateInstance(ScriptDeclaration scriptDeclaration, string methodDeclaration, string code);
    }
}