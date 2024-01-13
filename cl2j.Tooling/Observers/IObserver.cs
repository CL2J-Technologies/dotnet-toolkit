namespace cl2j.Tooling.Observers;

public interface IObserver<T>
{
    Task OnChangeAsync(T t);
}