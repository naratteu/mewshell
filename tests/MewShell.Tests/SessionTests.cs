using System.Diagnostics;
using static MewShell.Shell;

namespace MewShell.Tests;

/// <summary>
/// A session is where commands run and what they inherit, and it is a value: deriving one for a
/// directory or a window cannot change what any other task sees, which a global cd would.
/// </summary>
public class SessionTests
{
    [Fact]
    public async Task InterleavedCommandsEachSeeTheirOwnDirectory()
    {
        var root = Session.Default.In("/");
        var usr = Session.Default.In("/usr");

        var seen = await Task.WhenAll(Enumerable.Range(0, 20).Select(async i =>
            (await (i % 2 == 0 ? root : usr).Sh($"pwd")).Text() == (i % 2 == 0 ? "/" : "/usr")));

        Assert.All(seen, Assert.True);
    }

    [Fact]
    public async Task EnvironmentBelongsToTheSession()
    {
        var script = "echo ${GREETING:-none}";

        Assert.Equal("hi", (await Session.Default.With("GREETING", "hi").Sh($"sh -c {script}")).Text());
        Assert.Equal("none", (await Sh($"sh -c {script}")).Text());
    }

    [Fact]
    public async Task OnRunsTheSameCommandLineInAnotherSession()
    {
        var pwd = Sh($"pwd");

        Assert.Equal("/usr", (await pwd.On(Session.Default.In("/usr"))).Text());
    }

    [Fact]
    public async Task CancellingASessionKillsItsCommandsAndOnlyThose()
    {
        using var window = new CancellationTokenSource();
        var history = new List<string>();
        var scoped = Session.Default.Scoped(window.Token) with { Log = history.Add };

        var clock = Stopwatch.StartNew();
        var first = scoped.Sh($"sleep 10").RunAsync();
        var second = scoped.Sh($"sleep 10").RunAsync();
        var bystander = Sh($"sleep 1").RunAsync();

        await Task.Delay(200);
        window.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => second);
        Assert.InRange(clock.ElapsedMilliseconds, 0, 2000);

        Assert.Equal(0, (await bystander).ExitCode);
        Assert.Equal(2, history.Count);
    }

    [Fact]
    public async Task ACallsOwnTokenCancelsJustThatRun()
    {
        using var superseded = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Sh($"sleep 10").RunAsync(superseded.Token));
    }
}
