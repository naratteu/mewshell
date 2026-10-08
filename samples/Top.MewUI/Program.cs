using System.Diagnostics;
using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using MewShell;
using R3;

Application
    .Create()
    .UseMacOS()
    .UseMewVGMetal()
    .BuildMainWindow(() => new TopWindow())
    .Run();

/// <summary>
/// A tiny <c>top</c>: a line that polls the system every second, and a filter box that re-runs
/// <c>pgrep</c> on every edit -- the two shapes a GUI over a command line keeps needing.
/// </summary>
sealed class TopWindow : Window
{
    // Every command this window starts is cancelled, and so killed, when it closes.
    private readonly CancellationTokenSource _lifetime = new();
    private readonly CompositeDisposable _subscriptions = [];

    private readonly ObservableValue<string> _live = new("…");
    private readonly ObservableValue<string> _pattern = new("dotnet");
    private readonly ObservableValue<string> _matches = new("");
    private readonly ObservableValue<string> _status = new("");

    private readonly TextBox _filter = new();

    private IDispatcher? _ui;

    public TopWindow()
    {
        this.Resizable(640, 440, minWidth: 480, minHeight: 320);
        this.OnLoaded(Start);
        this.OnClosed(() =>
        {
            _subscriptions.Dispose();
            _lifetime.Cancel();
        });
    }

    protected override void OnBuild()
    {
        this.Title("Top — MewUI")
            .Content(
                new StackPanel()
                    .Vertical()
                    .Spacing(10)
                    .Margin(20)
                    .Children(
                        new TextBlock().Text("Mew Top").FontSize(22).Bold(),
                        new TextBlock().BindText(_live).FontFamily("Menlo"),
                        _filter.BindText(_pattern),
                        new TextBlock().BindText(_status).FontSize(12),
                        new TextBlock().BindText(_matches).FontFamily("Menlo").FontSize(11).TextWrapping(TextWrapping.NoWrap)));
    }

    /// <summary>Runs once the window is shown, so results always have a dispatcher to land on.</summary>
    private void Start()
    {
        _ui = Application.Current.Dispatcher;

        var shell = Session.Default.Scoped(_lifetime.Token) with
        {
            Log = line => Console.WriteLine($"[run] {line}"),
        };

        // Poll every second. Drop: a tick that arrives while a probe is still running is skipped
        // rather than stacking processes. DistinctUntilChanged: nothing re-renders unless it changed.
        Observable.Timer(TimeSpan.Zero, TimeSpan.FromSeconds(1))
            .SelectAwait((_, ct) => Probe(shell, ct), AwaitOperation.Drop)
            .DistinctUntilChanged()
            .Subscribe(value => OnUi(() => _live.Value = value))
            .AddTo(_subscriptions);

        // Re-run on every edit. Debounce: wait for typing to pause. Switch: a newer pattern cancels
        // the search in flight, and cancelling kills its pgrep.
        Changes(_pattern)
            .Debounce(TimeSpan.FromMilliseconds(250))
            .SelectAwait((pattern, ct) => Search(shell, pattern, ct), AwaitOperation.Switch)
            .Subscribe(result => OnUi(() => (_matches.Value, _status.Value) = result))
            .AddTo(_subscriptions);

        _filter.Focus();
    }

    private static async ValueTask<string> Probe(Session shell, CancellationToken ct)
    {
        var time = await shell.Sh($"date +%T").RunAsync(ct);
        var load = await shell.Sh($"sysctl -n vm.loadavg").RunAsync(ct);
        return $"{time.Text()}   load {load.Text().Trim('{', '}', ' ')}";
    }

    private static async ValueTask<(string Matches, string Status)> Search(Session shell, string pattern, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(pattern)) return ("", "Type part of a process name.");

        var clock = Stopwatch.StartNew();
        try
        {
            // pgrep exits 1 when nothing matches, which is an answer here, not a failure.
            var result = await shell.NoThrow().Sh($"pgrep -l {pattern}").RunAsync(ct);
            var lines = result.Text().Split('\n', StringSplitOptions.RemoveEmptyEntries);

            return (
                string.Join('\n', lines.Take(12)),
                $"{lines.Length} processes match \"{pattern}\"  ({clock.ElapsedMilliseconds} ms)");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"[killed] pgrep -l {pattern} -- superseded after {clock.ElapsedMilliseconds} ms");
            throw;
        }
    }

    /// <summary>An <see cref="ObservableValue{T}"/> as an R3 stream: its current value, then each change.</summary>
    private static Observable<T> Changes<T>(ObservableValue<T> source) => Observable.Create<T>(observer =>
    {
        void Push() => observer.OnNext(source.Value);

        source.Changed += Push;
        Push();
        return Disposable.Create(() => source.Changed -= Push);
    });

    /// <summary>Results arrive on pool threads; controls are only touched on the UI thread.</summary>
    private void OnUi(Action action) => _ui?.BeginInvoke(action);
}
