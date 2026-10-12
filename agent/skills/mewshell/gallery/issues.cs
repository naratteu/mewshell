#:package Aprillz.MewUI@0.22.1
#:package Naratteu.MewShell.MewUI@0.1.0-preview.1

// GitHub repositories → issues (with a search box) → issue body, from gh JSON. Run: dotnet run issues.cs [owner]

using System.Text.Json;
using System.Text.Json.Serialization;
using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using MewShell;
using MewShell.MewUI;
using static MewShell.Shell;

var owner = args.FirstOrDefault() ?? "naratteu";
var gh = Session.Default;

// What the user picks or types. Each one becomes an argument of a gh command.
var repo = new ObservableValue<Repo?>(null);
var search = new ObservableValue<string?>("");
var issue = new ObservableValue<Issue?>(null);

// gh prints JSON with --json; the generated context below parses it without reflection.
var repos = gh.Query(() => Sh($"gh repo list {owner} --limit 50 --json name,description,stargazerCount"),
    r => (IReadOnlyList<Repo>)JsonSerializer.Deserialize(r.Stdout, Json.Default.RepoArray)!);
var issues = gh.Query(repo, search, (r, s) => Sh($"gh issue list -R {owner}/{r.Name} --state all --limit 100 --search {s} --json number,title,state"),
    r => (IReadOnlyList<Issue>)JsonSerializer.Deserialize(r.Stdout, Json.Default.IssueArray)!);
var body = gh.Query(repo, issue, (r, i) => Sh($"gh issue view {i.Number} -R {owner}/{r.Name} --json body --jq .body"), r => r.Stdout);

Application.Create().UseThisOS()
    .BuildMainWindow(() => new Window().Title($"Issues — {owner}").Resizable(1100, 700).Content(
        new Grid().Columns("380,*").Rows("Auto,*,2*").Spacing(10).Margin(16).Children(
            // Network commands take a moment: say so, and say why when one fails.
            new TextBlock().BindText(repos.Running, busy => busy ? "Loading repositories…" : "").CenterVertical(),
            new GridView().Row(1).RowSpan(2).Bind(repos, repo).Columns(
                new GridViewColumn<Repo>().Header("Repository").StarWidth().Text(r => r.Name),
                new GridViewColumn<Repo>().Header("★").PixelWidth(40).Text(r => r.StargazerCount.ToString())),
            new TextBox().Column(1).BindText(search!),
            new ListBox().Row(1).Column(1).Bind(issues, issue, i => $"#{i.Number}  {i.State,-6}  {i.Title}"),
            new ScrollViewer().Row(2).Column(1).Content(
                new TextBlock().Top().BindText(body.Value, b => b ?? "").TextWrapping(TextWrapping.Wrap)),
            new TextBlock().Row(2).Column(1).BindText(issues.Error, e => e ?? "").VerticalAlignment(VerticalAlignment.Bottom))))
    .Run();

record Repo(string Name, string? Description, int StargazerCount);
record Issue(int Number, string Title, string State);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Repo[]))]
[JsonSerializable(typeof(Issue[]))]
partial class Json : JsonSerializerContext;

static class Platform
{
    // One file for every OS: register the host and renderer of the one it runs on.
    public static string Mono => OperatingSystem.IsWindows() ? "Consolas" : OperatingSystem.IsMacOS() ? "Menlo" : "monospace";

    public static ApplicationBuilder UseThisOS(this ApplicationBuilder app) =>
        OperatingSystem.IsWindows() ? app.UseWin32().UseDirect2D()
        : OperatingSystem.IsMacOS() ? app.UseMacOS().UseMewVGMetal()
        : app.UseX11().UseMewVGX11();
}
