namespace cl2j.DataStore
{
    /// <summary>
    ///     A store that holds a cache, and can be asked to fill it.
    ///
    ///     <para>
    ///     The point of this interface is that it has no type parameters. A registry knows every
    ///     store it was given, but the load interfaces are generic, so warming everything meant
    ///     naming each store again with its type arguments — a second copy of what registration
    ///     already said, which rots silently the first time somebody adds a store and forgets the
    ///     line. See issue #34.
    ///     </para>
    ///
    ///     <para>
    ///     What this deliberately does not carry is a policy. When to warm, how long to wait, and
    ///     whether a store that cannot be read should stop an application from starting are
    ///     decisions an application makes, not a library. An application that wants to be warm
    ///     before it serves waits on these from
    ///     <c>IHostedLifecycleService.StartingAsync</c> — <c>StartAsync</c> is too late, because
    ///     the web host opens its port from a <c>StartAsync</c> of its own.
    ///     </para>
    /// </summary>
    public interface IDataStoreWarmable
    {
        /// <summary>The name the store was registered under.</summary>
        string Name { get; }

        /// <summary>
        ///     Completes once the cache has been filled at least once.
        /// </summary>
        /// <remarks>
        ///     Takes no cancellation token on purpose: nothing underneath it can be cancelled, and
        ///     a token that is quietly ignored is worse than none. A caller that wants a bound puts
        ///     one around the wait — <c>Task.WhenAll(...).WaitAsync(budget)</c>.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        ///     The source could not be read, so there is nothing to warm. It is raised rather than
        ///     swallowed because whether that should stop an application is the caller's decision,
        ///     and it can only make it if it is told.
        /// </exception>
        Task WarmAsync();
    }

    /// <summary>
    ///     Something that knows which of the stores it holds can be warmed. Implemented by both
    ///     registries; a store registered without a cache is not offered, since it reads its source
    ///     on every call and there is nothing to fill.
    /// </summary>
    public interface IDataStoreWarmableSource
    {
        IReadOnlyCollection<IDataStoreWarmable> GetWarmable();
    }
}
