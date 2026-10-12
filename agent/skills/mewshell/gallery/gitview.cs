#:package Aprillz.MewUI@0.22.1
#:package Naratteu.MewShell.MewUI@0.1.0-preview.1

// Branches → commits → changed files → diff, plus a Fetch button. Run: dotnet run gitview.cs [repo]

using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using MewShell;
using MewShell.MewUI;
using static MewShell.Shell;

var git = Session.Default.In(args.FirstOrDefault() ?? ".");
var status = new ObservableValue<string>("");

// What the user picks. Each one becomes an argument of the next command.
var branch = new ObservableValue<string?>("main");
var commit = new ObservableValue<Commit?>(null);
var change = new ObservableValue<Change?>(null);

// What each command printed, kept current: a new pick re-runs it and kills the old run.
var branches = git.Query(() => Sh($"git for-each-ref --format=%(refname:short) refs/heads"), r => r.Lines());
var commits = git.Query(branch, b => Sh($"git log --date=short --format=%h%x09%ad%x09%s -n 300 {b}"), Commit.Parse);
var changes = git.Query(commit, c => Sh($"git show --numstat --format= {c.Hash}"), Change.Parse);
var diff = git.Query(commit, change, (c, f) => Sh($"git show --format= {c.Hash} -- {f.Path}"), r => r.Stdout);

async void Fetch()
{
    status.Value = "Fetching…";
    var result = await git.NoThrow().Sh($"git fetch --prune").RunAsync();
    status.Value = result.ExitCode == 0 ? "Fetched." : result.Stderr.Trim();
    branches.Refresh();
}

Application.Create().UseThisOS()
    .BuildMainWindow(() => new Window().Title("Git").Resizable(1100, 720).Content(
        new DockPanel().Spacing(10).Margin(16).Children(
            new StackPanel().Horizontal().Spacing(8).DockTop().Children(
                new ComboBox().Width(220).Bind(branches, branch),
                new Button().Content("Fetch").OnClick(Fetch),
                new TextBlock().BindText(status).CenterVertical()),
            new Grid().Columns("440,*").Rows("2*,3*").Spacing(10).Children(
                new ListBox().RowSpan(2).Bind(commits, commit, c => $"{c.Hash}  {c.Date}  {c.Subject}"),
                new GridView().Column(1).Bind(changes, change).Columns(
                    new GridViewColumn<Change>().Header("+").PixelWidth(56).Text(c => c.Added),
                    new GridViewColumn<Change>().Header("−").PixelWidth(56).Text(c => c.Deleted),
                    new GridViewColumn<Change>().Header("File").StarWidth().Text(c => c.Path)),
                new ScrollViewer().Row(1).Column(1).Content(
                    new TextBlock().Top().BindText(diff.Value, d => d ?? "").FontFamily(Platform.Mono).FontSize(11))))))
    .Run();

record Commit(string Hash, string Date, string Subject)
{
    public static IReadOnlyList<Commit> Parse(ShResult r) => [.. r.Fields().Select(f => new Commit(f[0], f[1], f[2]))];
}

record Change(string Added, string Deleted, string Path)
{
    public static IReadOnlyList<Change> Parse(ShResult r) => [.. r.Fields().Select(f => new Change(f[0], f[1], f[2]))];
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
