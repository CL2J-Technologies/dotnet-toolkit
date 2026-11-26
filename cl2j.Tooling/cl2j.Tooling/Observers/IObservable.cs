namespace cl2j.Tooling.Observers
{
    public interface IObservable<T>
    {
        bool Subscribe(IObserver<T> observer);

        Task NotifyAsync(T t);
    }
}