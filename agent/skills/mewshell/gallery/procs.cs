#:package Aprillz.MewUI@0.22.1
#:package Naratteu.MewShell.MewUI@0.1.0-preview.1

// Processes polled every two seconds, filtered by a text box, with Kill on the selected row. Run: dotnet run procs.cs

using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using MewShell;
using MewShell.MewUI;
using static MewShell.Shell;

var filter = new ObservableValue<string?>("");
var picked = new ObservableValue<Proc?>(null);
var status = new ObservableValue<string>("");

// The filter is an input, so typing re-runs the query; the timer re-runs it every two seconds.
var procs = Session.Default.Query(filter, f => Sh($"ps -axo pid=,pcpu=,rss=,comm="), r => Proc.Parse(r, filter.Value ?? ""));
var poll = new DispatcherTimer(TimeSpan.FromSeconds(2));
poll.Tick += procs.Refresh;
poll.Start();

async void Kill()
{
    if (picked.Value is not { } p) return;
    var result = await Session.Default.NoThrow().Sh($"kill {p.Pid}").RunAsync();
    status.Value = result.ExitCode == 0 ? $"Sent TERM to {p.Pid}." : result.Stderr.Trim();
    procs.Refresh();
}

Application.Create().UseThisOS()
    .BuildMainWindow(() => new Window().Title("Processes").Resizable(820, 560).Content(
        new DockPanel().Spacing(8).Margin(16).Children(
            new StackPanel().Horizontal().Spacing(8).DockTop().Children(
                new TextBox().Width(240).Placeholder("Filter by name").BindText(filter!),
                new Button().Content("Kill").OnClick(Kill).Bind(UIElement.IsEnabledProperty, picked, p => p is not null),
                new TextBlock().BindText(status).CenterVertical()),
            new GridView().Bind(procs, picked).Columns(
                new GridViewColumn<Proc>().Header("PID").PixelWidth(80).Text(p => p.Pid),
                new GridViewColumn<Proc>().Header("CPU %").PixelWidth(70).Text(p => p.Cpu),
                new GridViewColumn<Proc>().Header("MB").PixelWidth(70).Text(p => p.Mb),
                new GridViewColumn<Proc>().Header("Command").StarWidth().Text(p => p.Command)))))
    .Run();

record Proc(string Pid, string Cpu, string Mb, string Command)
{
    // A row is the same process across refreshes even when its numbers move, so the selection survives polling.
    public virtual bool Equals(Proc? other) => other?.Pid == Pid;
    public override int GetHashCode() => Pid.GetHashCode();

    // ps pads its columns with spaces, and the command may contain spaces too: split off three fields only.
    public static IReadOnlyList<Proc> Parse(ShResult r, string filter) =>
    [
        .. r.Lines()
            .Select(line => line.Trim().Split(' ', 4, StringSplitOptions.RemoveEmptyEntries))
            .Where(f => f.Length == 4 && f[3].Contains(filter, StringComparison.OrdinalIgnoreCase))
            .Select(f => new Proc(f[0], f[1], $"{long.Parse(f[2]) / 1024}", f[3]))
            .OrderByDescending(p => double.Parse(p.Cpu, System.Globalization.CultureInfo.InvariantCulture)),
    ];
}

static class Platform
{
    // One file for every OS: register the host and renderer of the one it runs on.
    public static string Mono => OperatingSystem.IsWindows() ? "Consolas" : OperatingSystem.IsMacOS() ? "Menlo" : "monospace";

    public static ApplicationBuilder UseThisOS(this ApplicationBuilder app) =>
        OperatingSystem.IsWindows() ? app.UseWin32().UseDirect2D()
        : OperatingSystem.IsMacOS() ? app.UseMacOS().UseMewVGMetal()
        : app.UseX11().UseMewVGX11();
}
