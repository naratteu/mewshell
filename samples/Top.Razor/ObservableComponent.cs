using Naratteu.MewUI.Razor;
using R3;

namespace Top.Razor;

/// <summary>A hook for R3 streams.</summary>
public abstract class ObservableComponent : HookComponent
{
    /// <summary>
    /// The latest value an R3 stream produced. Subscribed when the component mounts, disposed when
    /// it leaves; values may arrive on any thread, because the state setter accepts that.
    /// </summary>
    protected T UseObservable<T>(Func<Observable<T>> create, T initial)
    {
        var (value, set) = UseState(initial);
        UseEffect(() => create().Subscribe(set).Dispose);
        return value;
    }
}
