# CSharp Scripting

Small, powerful and efficient library to compile and execute dynamic C# code at runtime in your .NET application.

Features:

- Statement execution.
- Expression evaluation, with an optional typed input and a typed result.
- Compile once, execute many times: `Script.Create` compiles, `Execute` only calls the compiled method.
- Compilation errors reported as a `ScriptException` that carries the compiler message and the generated source.
- Override default namespaces and assemblies used in the compilation.

Targets .NET 10 (`net10.0`). It compiles with Roslyn (`Microsoft.CodeAnalysis.CSharp`).

```powershell
dotnet add package cl2j.Scripting
```

# Getting started

The following code will return the string `string1`.

```cs
using cl2j.Scripting;

var options = ScriptOptions.Create<string>(ScriptOptions.AddReturn(@"""string1"""));
var script = Script.Create(options);

var result = script.Execute<string>();
```

`ScriptOptions.AddReturn(expression)` wraps an expression into `return <expression>;`. Your code is the body of a generated method: write statements, and `return` a value when you ask for a result.

## Execute an expression

Let's start with a simple expression execution:

```cs
var expression = @"Debug.WriteLine(""Test"");";
```

To execute the preceding code, we need to add the `System.Diagnostics` namespace. The following code will allow the execution of the expression:

```cs
var options = ScriptOptions.Create(expression);
options.AddNamespaces("System.Diagnostics");

var script = Script.Create(options);
script.Execute();
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
var options = ScriptOptions.Create<TestScriptContext, bool>(ScriptOptions.AddReturn(expression));
options.AddAssembly(typeof(TestScriptContext));

var script = Script.Create(options);

var result = script.Execute<TestScriptContext, bool>(context);
```

We need to create the `ScriptOptions` with the input and output types and add our assembly that contains the class `TestScriptContext`, since the generated code has to reference it.

The `ScriptOptions` created this way already contain the default namespaces and assemblies. Since we added our assembly containing the class, the code will compile.

> Note: When a input is passed, the code generated always name it `context`.

`Script.Create` is the expensive step. Keep the `Script` and call `Execute` as many times as needed instead of creating it for every evaluation.

## Compilation errors

Code that does not compile makes `Script.Create` throw a `ScriptException` (namespace `cl2j.Scripting.Exceptions`). The message carries the compiler diagnostics (for example `CS0029`), and `CompileSource` holds the complete generated source that was compiled.

## Default namespaces and assemblies

The `Create` methods of `ScriptOptions` call `AddDefault()` so you don't have to add them:

- Namespaces : **System, System.Text, System.Collections, System.Threading.Tasks, System.Linq**
- Assemblies : the assemblies loaded in the current application domain, and the assemblies they reference

`AddDefault()` also writes one summary line to the console.

### Change the default namespace and assemblies

Pass `addDefault: false` to `ScriptOptions.Create...` to start from nothing, then add what the code needs:

```cs
var options = ScriptOptions.Create<int>("return Math.Max(1, 2);", addDefault: false);
options.AddNamespaces("System");
options.AddAssembly(typeof(object));   // or AddAssembly(Assembly)

var value = Script.Create(options).Execute<int>();
```

`AddNamespaces(params string[])` and `AddAssembly(Type | Assembly)` add to what is already there. `AddAssembly` returns `false` when a reference to the assembly could not be created.

Other `ScriptOptions` members: `CompileWithDebug`, `ClassNamespace` (the namespace of the generated class), `MethodName` (default `Execute`), `InputName` (default `context`) and `InstanceCreator`, to replace how the compiled class is instantiated.
