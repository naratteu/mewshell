using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using MewShell;
using MewShell.MewUI;
using static MewShell.Shell;

var repository = args.Length > 0 ? args[0] : Environment.CurrentDirectory;

Application
    .Create()
    .UseMacOS()
    .UseMewVGMetal()
    .BuildMainWindow(() => new GitWindow(repository))
    .Run();

sealed class GitWindow : Window
{
    private readonly Session _git;

    // What the user has picked. Each one is an argument of the next command.
    private readonly ObservableValue<string?> _branch = new("main");
    private readonly ObservableValue<Commit?> _commit = new(null);
    private readonly ObservableValue<Change?> _change = new(null);

    // Each command's output, kept current.
    private readonly Query<IReadOnlyList<string>> _branches;
    private readonly Query<IReadOnlyList<Commit>> _commits;
    private readonly Query<IReadOnlyList<Change>> _changes;
    private readonly Query<string> _diff;

    private readonly ObservableValue<string> _status = new("");

    public GitWindow(string repository)
    {
        // Every command line is echoed, so the terminal shows what each pick ran.
        _git = Session.Default.In(repository) with { Log = line => Console.WriteLine($"[run] {line}") };

        // Read top to bottom, this is the whole data flow: a picked value goes in as an argument,
        // and a new pick re-runs the command, killing the old run if it is still going.
        _branches = _git.Query(() => Sh($"git for-each-ref --format=%(refname:short) refs/heads"), r => r.Lines());
        _commits = _git.Query(_branch, branch => Sh($"git log --date=short --format=%h%x09%ad%x09%an%x09%s -n 300 {branch}"), Commit.Parse);
        _changes = _git.Query(_commit, commit => Sh($"git show --numstat --format= {commit.Hash}"), Change.Parse);
        _diff = _git.Query(_commit, _change, (commit, change) => Sh($"git show --format= {commit.Hash} -- {change.Path}"), Head);

        this.Resizable(1100, 720, minWidth: 800, minHeight: 500);
    }

    protected override void OnBuild() => this
        .Title("Git — MewUI")
        .Content(new DockPanel().Spacing(10).Margin(16).Children(
            new StackPanel().Horizontal().Spacing(8).DockTop().Children(
                new ComboBox().Width(220).Bind(_branches, _branch),
                new Button().Content("Fetch").OnClick(Fetch),
                new TextBlock().BindText(_status).CenterVertical()),
            new Grid().Columns("440,*").Rows("2*,3*").Spacing(10).Children(
                new ListBox().RowSpan(2).Bind(_commits, _commit, c => $"{c.Hash}  {c.Date}  {c.Subject}"),
                new GridView().Column(1).Bind(_changes, _change).Columns(
                    new GridViewColumn<Change>().Header("+").PixelWidth(56).Text(c => c.Added),
                    new GridViewColumn<Change>().Header("−").PixelWidth(56).Text(c => c.Deleted),
                    new GridViewColumn<Change>().Header("File").StarWidth().Text(c => c.Path)),
                new ScrollViewer().Row(1).Column(1).Content(
                    new TextBlock().BindText(_diff.Value, diff => diff ?? "").FontFamily("Menlo").FontSize(11)))));

    // A button runs a command, then refreshes what depends on it.
    private async void Fetch()
    {
        _status.Value = "Fetching…";
        var result = await _git.NoThrow().Sh($"git fetch --prune").RunAsync();
        _status.Value = result.ExitCode == 0 ? $"Fetched at {DateTime.Now:T}" : result.Stderr.Trim();
        _branches.Refresh();
    }

    private static string Head(ShResult diff) => string.Join('\n', diff.Stdout.Split('\n').Take(400));
}

sealed record Commit(string Hash, string Date, string Author, string Subject)
{
    public static IReadOnlyList<Commit> Parse(ShResult log) =>
        [.. log.Fields().Select(f => new Commit(f[0], f[1], f[2], f[3]))];
}

sealed record Change(string Added, string Deleted, string Path)
{
    public static IReadOnlyList<Change> Parse(ShResult numstat) =>
        [.. numstat.Fields().Select(f => new Change(f[0], f[1], f[2]))];
}
