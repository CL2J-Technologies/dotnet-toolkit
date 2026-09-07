namespace cl2j.Tooling.Observers;

public class Observable<T> : IObservable<T>
{
    private readonly List<IObserver<T>> observers = [];

    public bool Subscribe(IObserver<T> observer)
    {
        if (!observers.Contains(observer))
        {
            observers.Add(observer);
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Notifies every observer exactly once, whatever the others do, and reports what failed.
    ///
    ///     <para>
    ///     This used to catch around the whole loop and then run the whole loop again, inside a
    ///     bare <c>catch { }</c>. So one broken observer made every observer before it perform its
    ///     side effect a second time, and the failure itself was never reported anywhere — a
    ///     permanently broken observer was invisible. See issue #31.
    ///     </para>
    /// </summary>
    /// <exception cref="AggregateException">
    ///     One or more observers threw. Every observer has still been notified.
    /// </exception>
    public async Task NotifyAsync(T t)
    {
        List<Exception>? failures = null;

        foreach (var observer in observers)
        {
            try
            {
                await observer.OnChangeAsync(t);
            }
            catch (Exception ex)
            {
                //Collected rather than rethrown here: an observer that fails is not a reason for
                //the ones after it to miss the notification.
                (failures ??= []).Add(ex);
            }
        }

        if (failures is not null)
            throw new AggregateException("One or more observers failed to handle the notification.", failures);
    }
}