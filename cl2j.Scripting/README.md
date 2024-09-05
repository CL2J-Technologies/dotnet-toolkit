# CSharp Scripting

Small, porwerfull and efficient library to compile and execute dynamic C# code at runtime in your .NET application.

Features:

- Expression execution.
- Expression evaluation.
- Built-in caching provided quick execution of the same expression.
- Stateless, so it's Thread safe and can be used in multi-thread application like Web applications.
- Override default namespaces and assemblies used in the compilation.

Supports : .NET 6+

# Getting started

The following code will return the string `string1`.

```cs
var expression = ScriptEngine.AddReturn(@"""string1""");
var result = ScriptEngine.Create().Execute<string>(expression);
```

## Execute an expression

Let's start with a simple expression execution:

```cs
var expression = @"Debug.WriteLine(""Test"");";
```

To execute the preceding code, we need to add the `System.Diagnostics` namespace. The following code will allow the execution of the expression:

```cs
var scriptDefinition = ScriptDeclaration.CreateDefault();
scriptDefinition.AddNamespaces("System.Diagnostics");

var scriptEngine = ScriptEngine.Create(scriptDefinition);
scriptEngine.Execute(expression);
```

## Evaluation an expression

The Library allow the execution of code that take inputs used by the code.

1. Let's create out input model and a instance with values

```cs
public class TestScriptContext
{
    public string? Text { get; set; }
    public int Value { get; set; }
}

var context = new TestScriptContext
{
    Text = "Text1",
    Value = 5
};
```

2. Define the expression to be evaluated

```cs
var expression = @"context.Text == ""Text1""";
```

3. Evaluate the expression

```cs
var scriptDefinition = ScriptDeclaration.CreateDefault();
scriptDefinition.AddAssembly(typeof(TestScriptContext));

var scriptEngine = ScriptEngine.Create(scriptDefinition);

var result = scriptEngine.Execute<TestScriptContext, bool>(ScriptEngine.AddReturn(expression), context);
```

We need to create a `ScriptDeclaration` and add our assembly that contains the class `TestScriptContext`.

After we create the `ScriptEngine` using the default namespaces and assemblies. Since we added our assembly containing the class, the code will compile.

> Note: When a input is passed, the code generated always name it `context`.

## Default namespaces and assemblies

ScriptDeclaration.CreateDefault() add the following dependencies so you don't have to add them:

- Namespaces : **System, System.Text, System.Collections, System.Threading.Tasks,System.Linq**
- Asemblies : System.Private.CoreLib.dll, System.Linq.dll

### Change the default namespace and assemblies

The `ScriptEngine` offer a static method SetDefaultScriptDeclaration to overrid the default configurations.
