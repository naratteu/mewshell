using MewShell;
using static MewShell.Shell;

// The core, published with NativeAOT and then executed: each check runs a real process.
var failures = 0;

void Check<T>(string what, T actual, T expected)
{
    var ok = EqualityComparer<T>.Default.Equals(actual, expected);
    if (!ok) failures++;
    Console.WriteLine($"{(ok ? "ok  " : "FAIL")} {what}: {actual}");
}

var evil = "a b; rm -rf / $(whoami)";
Check("a hostile value stays one argument", (await Sh($"printf %s {evil}")).Stdout, evil);
Check("pipes", (await (Sh($"seq 1 5") | Sh($"tail -n 2") | Sh($"paste -sd+ -"))).Text(), "4+5");
Check("a session's directory", (await Session.Default.In("/").Sh($"pwd")).Text(), "/");
Check("a session's environment", (await Session.Default.With("V", "x").Sh($"printenv V")).Text(), "x");
Check("NoThrow returns the exit code", (await Session.Default.NoThrow().Sh($"false")).ExitCode, 1);

var noisy = "head -c 200000 /dev/zero | tr '\\0' 'w' >&2; echo done";
var lines = new List<string>();
using (var guard = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
{
    await foreach (var line in Session.Default.Scoped(guard.Token).Sh($"sh -c {noisy}").StreamAsync()) lines.Add(line);
}
Check("a stderr-heavy stream completes", string.Join(",", lines), "done");

using (var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(200)))
{
    var killed = false;
    try { await Session.Default.Scoped(cancel.Token).Sh($"sleep 10").RunAsync(); }
    catch (OperationCanceledException) { killed = true; }
    Check("cancelling the session kills its process", killed, true);
}

Check("a different value is a different key", Sh($"git log {"main"}").Key == Sh($"git log {"dev"}").Key, false);

Console.WriteLine(failures == 0 ? "all checks passed" : $"{failures} checks failed");
return failures == 0 ? 0 : 1;
