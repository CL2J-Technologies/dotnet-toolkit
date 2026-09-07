using Xunit;

//xUnit runs test classes in parallel by default. Much of what this package keeps is process-wide
//and mutable — the command builder registry, the descriptor cache, ConnectionExtensions.Logger and
//DatabaseOptions — so a class that registers a builder can change what another class resolves, in
//the middle of it doing so.
//
//That is not hypothetical here. RegistrationTests asserts which registration answers, while every
//other class calls SqlServer.Register() in its setup. The window between two adjacent statements is
//small enough that the suite passes run after run, which is exactly the kind of test that fails
//once, on the runner, at an unhelpful moment.
//
//The concurrency this used to provide incidentally is covered on purpose now:
//CommandBuilderFactoryConcurrencyTests spawns its own threads and reproduces the race it guards.
//The suite runs in tens of milliseconds either way.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
