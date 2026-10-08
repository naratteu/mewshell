using System.Runtime.CompilerServices;
using CliWrap;
using CliWrap.Buffered;
using CliWrap.EventStream;

namespace MewShell;

/// <summary>A CliWrap command bound to the session it came from. <see cref="Raw"/> is the escape hatch.</summary>
public sealed class ShCommand
{
    internal ShCommand(IReadOnlyList<string> argv, Session session)
    {
        Argv = argv;
        Session = session;
        Raw = session.Build(argv);
    }

    private ShCommand(Command raw, Session session)
    {
        Raw = raw;
        Session = session;
    }

    public Command Raw { get; }

    public Session Session { get; }

    /// <summary>The arguments, program first. Null for a pipeline, which is several programs.</summary>
    public IReadOnlyList<string>? Argv { get; }

    /// <summary>
    /// What would run: arguments, directory and environment. Two commands with the same key do the
    /// same thing, which is what a UI uses to decide whether to run again.
    /// </summary>
    public string Key => string.Join('\u001f', Argv ?? [Raw.ToString()])
        + '\u001e' + Session.Cwd
        + '\u001e' + string.Join(',', Session.Env.OrderBy(e => e.Key).Select(e => $"{e.Key}={e.Value}"));

    /// <summary>The same command line, run in another session.</summary>
    public ShCommand On(Session session) => Argv is not null
        ? new ShCommand(Argv, session)
        : throw new InvalidOperationException("A pipeline cannot move to another session.");

    public static ShCommand operator |(ShCommand left, ShCommand right) => new(left.Raw | right.Raw, left.Session);

    public TaskAwaiter<ShResult> GetAwaiter() => RunAsync().GetAwaiter();

    /// <summary>
    /// Cancelled by either token: the session's (the window closed) or this call's (a newer run
    /// superseded it). Cancellation kills the process rather than abandoning it.
    /// </summary>
    public async Task<ShResult> RunAsync(CancellationToken cancellation = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(Session.Token, cancellation);
        Session.Log?.Invoke(Display);
        var result = await Raw.ExecuteBufferedAsync(linked.Token);
        return new ShResult(result.ExitCode, result.StandardOutput, result.StandardError);
    }

    /// <summary>Standard output as it arrives; standard error is drained alongside it, so neither can stall the other.</summary>
    public async IAsyncEnumerable<string> StreamAsync([EnumeratorCancellation] CancellationToken cancellation = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(Session.Token, cancellation);
        Session.Log?.Invoke(Display);
        await foreach (var e in Raw.ListenAsync(linked.Token))
        {
            if (e is StandardOutputCommandEvent output) yield return output.Text;
        }
    }

    private string Display => Raw.ToString();
}
