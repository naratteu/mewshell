using System.Diagnostics;
using CliWrap.Exceptions;
using static MewShell.Shell;

namespace MewShell.Tests;

public class RunTests
{
    [Fact]
    public async Task OutputIsCaptured()
    {
        Assert.Equal("hello", (await Sh($"echo hello")).Text());
    }

    [Fact]
    public async Task ProcessesPipeIntoEachOtherWithoutAShell()
    {
        var result = await (Sh($"seq 1 5") | Sh($"tail -n 2") | Sh($"paste -sd+ -"));

        Assert.Equal("4+5", result.Text());
    }

    [Fact]
    public async Task AFailureThrowsUnlessTheSessionSaysOtherwise()
    {
        await Assert.ThrowsAsync<CommandExecutionException>(async () => await Sh($"false"));

        Assert.Equal(1, (await Session.Default.NoThrow().Sh($"false")).ExitCode);
    }

    [Fact]
    public async Task OutputParsesIntoLinesAndFields()
    {
        var script = "printf 'a\\t1\\nb\\t2\\n'";
        var result = await Sh($"sh -c {script}");

        Assert.Equal(["a\t1", "b\t2"], result.Lines());
        Assert.Equal(["b", "2"], result.Fields().Last());
    }

    [Fact]
    public async Task AStreamThatWritesALotToStandardErrorStillCompletes()
    {
        // A hand-rolled Process reader that only drains stdout deadlocks here: the child blocks on a
        // full stderr pipe while the reader waits for stdout.
        var noisy = "head -c 200000 /dev/zero | tr '\\0' 'w' >&2; echo done";
        using var guard = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var lines = new List<string>();
        await foreach (var line in Session.Default.Scoped(guard.Token).Sh($"sh -c {noisy}").StreamAsync())
        {
            lines.Add(line);
        }

        Assert.Equal(["done"], lines);
    }

    [Fact]
    public async Task StreamedLinesArriveWhileTheProcessIsStillRunning()
    {
        var slow = "echo one; sleep 1; echo two";
        var clock = Stopwatch.StartNew();
        var first = -1L;

        await foreach (var _ in Sh($"sh -c {slow}").StreamAsync())
        {
            if (first < 0) first = clock.ElapsedMilliseconds;
        }

        Assert.InRange(first, 0, 700);
    }
}
