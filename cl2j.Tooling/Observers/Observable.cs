namespace cl2j.Tooling.Observers;

public class Observable<T> : IObservable<T>
{
    private readonly List<IObserver<T>> observers = new();

    public bool Subscribe(IObserver<T> observer)
    {
        if (!observers.Contains(observer))
        {
            observers.Add(observer);
            return true;
        }

        return false;
    }

    public async Task NotifyAsync(T t)
    {
        try
        {
            foreach (var observer in observers)
                await observer.OnChangeAsync(t);
        }
        catch
        {
            try
            {
                foreach (var observer in observers)
                    await observer.OnChangeAsync(t);
            }
            catch { }
        }
    }
}