using Aprillz.MewUI;

namespace MewShell.MewUI;

/// <summary>
/// The output of a command line, kept current. It runs once when created, again whenever an input
/// changes, and again on <see cref="Refresh"/>; a run still in flight is cancelled first, which
/// kills its process.
/// </summary>
public sealed class Query<T>
{
    private readonly Session _session;
    private readonly Func<ShCommand?> _build;
    private readonly Func<ShResult, T> _parse;
    private CancellationTokenSource? _current;

    internal Query(Session session, Func<ShCommand?> build, Func<ShResult, T> parse)
    {
        _session = session.NoThrow();
        _build = build;
        _parse = parse;
        Refresh();
    }

    /// <summary>The parsed output, or the default while there is no command to run.</summary>
    public ObservableValue<T?> Value { get; } = new(default);

    public ObservableValue<bool> Running { get; } = new(false);

    /// <summary>Standard error of a run that failed, or why it could not be parsed.</summary>
    public ObservableValue<string?> Error { get; } = new(null);

    public void Refresh()
    {
        _current?.Cancel();

        // Commands written with the global Sh(...) run in this query's session -- its directory,
        // environment and lifetime -- so the builder stays a plain command line.
        if (_build()?.On(_session) is not { } command)
        {
            _current = null;
            Ui.Post(() => (Value.Value, Error.Value, Running.Value) = (default, null, false));
            return;
        }

        var run = _current = CancellationTokenSource.CreateLinkedTokenSource(_session.Token);
        Ui.Post(() => Running.Value = true);

        _ = Task.Run(async () =>
        {
            T? value = default;
            string? error = null;
            try
            {
                var result = await command.RunAsync(run.Token);
                if (result.ExitCode == 0) value = _parse(result);
                else error = result.Stderr.Trim();
            }
            catch (OperationCanceledException) when (run.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                error = exception.Message;
            }

            // A newer run may have started while this one finished; only the newest lands.
            Ui.Post(() =>
            {
                if (run.IsCancellationRequested) return;
                (Value.Value, Error.Value, Running.Value) = (value, error, false);
            });
        });
    }
}

public static class QueryExtensions
{
    /// <summary>A query with no inputs: runs now, and on <see cref="Query{T}.Refresh"/>.</summary>
    public static Query<T> Query<T>(this Session session, Func<ShCommand> command, Func<ShResult, T> parse)
        => new(session, command, parse);

    /// <summary>A query that follows one value. While it is null there is nothing to run.</summary>
    public static Query<T> Query<TIn, T>(
        this Session session, ObservableValue<TIn?> input, Func<TIn, ShCommand> command, Func<ShResult, T> parse)
        where TIn : class
    {
        var query = new Query<T>(session, () => input.Value is { } value ? command(value) : null, parse);
        input.Changed += query.Refresh;
        return query;
    }

    /// <summary>A query that follows two values; it runs only once both are set.</summary>
    public static Query<T> Query<TA, TB, T>(
        this Session session, ObservableValue<TA?> first, ObservableValue<TB?> second,
        Func<TA, TB, ShCommand> command, Func<ShResult, T> parse)
        where TA : class
        where TB : class
    {
        var query = new Query<T>(
            session,
            () => (first.Value, second.Value) is ({ } a, { } b) ? command(a, b) : null,
            parse);
        first.Changed += query.Refresh;
        second.Changed += query.Refresh;
        return query;
    }
}
