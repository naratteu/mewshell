using System.Collections.Concurrent;
using Aprillz.MewUI;

namespace MewShell.MewUI;

/// <summary>
/// Posts to MewUI's UI thread. A window's constructor runs before the application has a
/// dispatcher, so a query created there can finish first; such work waits, in order, for it.
/// </summary>
internal static class Ui
{
    private static readonly ConcurrentQueue<Action> Early = new();
    private static int _draining;

    public static void Post(Action work)
    {
        if (Dispatcher() is { } dispatcher && Volatile.Read(ref _draining) == 0 && Early.IsEmpty)
        {
            dispatcher.BeginInvoke(work);
            return;
        }

        Early.Enqueue(work);
        if (Interlocked.CompareExchange(ref _draining, 1, 0) == 0) _ = DrainWhenReadyAsync();
    }

    private static IDispatcher? Dispatcher() => Application.IsRunning ? Application.Current.Dispatcher : null;

    private static async Task DrainWhenReadyAsync()
    {
        while (true)
        {
            IDispatcher? dispatcher;
            while ((dispatcher = Dispatcher()) is null) await Task.Delay(5).ConfigureAwait(false);

            while (Early.TryDequeue(out var work)) dispatcher.BeginInvoke(work);
            Volatile.Write(ref _draining, 0);

            if (Early.IsEmpty || Interlocked.CompareExchange(ref _draining, 1, 0) != 0) return;
        }
    }
}
