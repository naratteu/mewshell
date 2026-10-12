# Patterns

Every snippet here comes from one of the gallery apps, which were run and published with NativeAOT. The gallery is the source of truth for the surrounding code.

## Where commands run

```csharp
var git = Session.Default.In(args.FirstOrDefault() ?? ".");   // working directory from the command line
var quiet = git.With("NO_COLOR", "1").With("GIT_TERMINAL_PROMPT", "0");
var traced = Session.Default with { Log = Console.WriteLine };  // print every command line as it starts
```

A `Session` is immutable. Derive one per tool or folder and build queries and actions from it.

## A list, then its details (gitview.cs)

```csharp
var commit = new ObservableValue<Commit?>(null);
var change = new ObservableValue<Change?>(null);

var commits = git.Query(branch, b => Sh($"git log --date=short --format=%h%x09%ad%x09%s -n 300 {b}"), Commit.Parse);
var changes = git.Query(commit, c => Sh($"git show --numstat --format= {c.Hash}"), Change.Parse);

new ListBox().Bind(commits, commit, c => $"{c.Hash}  {c.Date}  {c.Subject}")
new GridView().Bind(changes, change).Columns(...)
```

While an input is `null` the query does not run and its value is empty. Picking a different commit empties the file pick, because that file is not among the new rows, so the diff below it clears too.

## Tab-separated output

Ask the tool to separate fields with tabs: `%x09` in git formats, `\t` in most `--format` templates. Then parse with `r.Fields()`:

```csharp
record Commit(string Hash, string Date, string Subject)
{
    public static IReadOnlyList<Commit> Parse(ShResult r) => [.. r.Fields().Select(f => new Commit(f[0], f[1], f[2]))];
}
```

Some output is padded with spaces and has a last column that may itself contain spaces, like `ps`. Split off only the leading fields:

```csharp
line.Trim().Split(' ', 4, StringSplitOptions.RemoveEmptyEntries)
```

## A preset choice (gitview.cs)

```csharp
var branch = new ObservableValue<string?>("main");
new ComboBox().Width(220).Bind(branches, branch)
```

The preset survives until the rows arrive. If it is not among them, it becomes `null`.

## JSON output (issues.cs)

```csharp
var repos = gh.Query(() => Sh($"gh repo list {owner} --limit 50 --json name,description,stargazerCount"),
    r => (IReadOnlyList<Repo>)JsonSerializer.Deserialize(r.Stdout, Json.Default.RepoArray)!);

record Repo(string Name, string? Description, int StargazerCount);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Repo[]))]
partial class Json : JsonSerializerContext;
```

Some tools wrap the list in an object, such as `brew info --json=v2`, which prints `{"formulae": [...], "casks": [...]}`. Model the wrapper too (brewview.cs):

```csharp
var info = brew.Query(formula, f => Sh($"{exe} info --json=v2 {f.Name}"),
    r => JsonSerializer.Deserialize(r.Stdout, Json.Default.InfoResult)!.Formulae.FirstOrDefault());

record InfoResult(Info[] Formulae);
record Info(string Name, string? Desc, string? Homepage, Versions Versions, string[] Dependencies);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(InfoResult))]
partial class Json : JsonSerializerContext;
```

A query's value need not be a list. Here it is one record, shown in a details pane with one `BindText` per field.

Tools that print one JSON object per line, like `docker ... --format {{json .}}`, need each line parsed:

```csharp
r => [.. r.Lines().Select(l => JsonSerializer.Deserialize(l, Json.Default.Container)!)]
```

with `[JsonSerializable(typeof(Container))]`. Use `PropertyNamingPolicy` to match the tool's keys: CamelCase, SnakeCaseLower or KebabCaseLower. Use `[JsonPropertyName("Names")]` for keys that fit no policy.

## A text box as an argument (issues.cs)

```csharp
var search = new ObservableValue<string?>("");
new TextBox().BindText(search!)
var issues = gh.Query(repo, search, (r, s) => Sh($"gh issue list -R {owner}/{r.Name} --search {s} --json number,title,state"), parse);
```

Every keystroke re-runs the query and kills the previous run, so only the newest result lands. The `!` tells the compiler that the nullable value is fine for `BindText`.

`Bind` takes a query, so filtering the rows always means running the command again. That is fine for a command that answers in well under a second. For a slower one, bind the text box to a draft value and copy it into the real input from a Search button:

```csharp
var draft = new ObservableValue<string>("");
new TextBox().BindText(draft)
new Button().Content("Search").OnClick(() => search.Value = draft.Value)
```

## Two inputs (issues.cs)

```csharp
var body = gh.Query(repo, issue, (r, i) => Sh($"gh issue view {i.Number} -R {owner}/{r.Name} --json body --jq .body"), r => r.Stdout);
```

It runs only when both inputs are set.

## A toggle, or more than two inputs

A `bool` cannot be a query input, and a query takes at most two inputs. Use the overload with no inputs, read the values inside it, and refresh on each change. The toggle may even switch the command (brewview.cs):

```csharp
var outdatedOnly = new ObservableValue<bool>(false);
var formulae = brew.Query(
    () => outdatedOnly.Value ? Sh($"{exe} outdated --formula --verbose") : Sh($"{exe} list --formula --versions"),
    r => Formula.Parse(r, filter.Value ?? ""));
filter.Changed += formulae.Refresh;
outdatedOnly.Changed += formulae.Refresh;

new CheckBox().Content("Outdated only").BindIsChecked(outdatedOnly)
```

The general form:

```csharp
var q = tool.Query(() => Sh($"tool list --a {a.Value} --b {b.Value} --c {c.Value}"), parse);
a.Changed += q.Refresh;
b.Changed += q.Refresh;
c.Changed += q.Refresh;
```

## Loading and error lines (issues.cs, brewview.cs)

```csharp
new TextBlock().BindText(repos.Running, busy => busy ? "Loading repositories…" : "")
new TextBlock().DockTop().BindText(formulae.Error, e => e ?? "").BindIsVisible(formulae.Error, e => e is not null).TextWrapping(TextWrapping.Wrap)
```

Give the error line its own row under the toolbar, as with `DockTop()` above. A long message in a horizontal toolbar runs off the edge.

`Error` holds stderr of a run that exited non-zero, or the message if the program could not start or its output could not be parsed.

## An action button (gitview.cs, procs.cs)

```csharp
async void Fetch()
{
    status.Value = "Fetching…";
    var result = await git.NoThrow().Sh($"git fetch --prune").RunAsync();
    status.Value = result.ExitCode == 0 ? "Fetched." : result.Stderr.Trim();
    branches.Refresh();
}

new Button().Content("Fetch").OnClick(Fetch)
```

Enable a button only when it can act:

```csharp
new Button().Content("Kill").OnClick(Kill).Bind(UIElement.IsEnabledProperty, picked, p => p is not null)
```

For a destructive action the user did not explicitly ask for, show what will run in the status line first, or add a confirmation.

## Polling (procs.cs)

```csharp
var procs = Session.Default.Query(filter, f => Sh($"ps -axo pid=,pcpu=,rss=,comm="), r => Proc.Parse(r, filter.Value ?? ""));
var poll = new DispatcherTimer(TimeSpan.FromSeconds(2));
poll.Tick += procs.Refresh;
poll.Start();
```

A tick that arrives while a run is in flight cancels it and starts again. Keep the interval longer than the command takes.

Rows that are refreshed while selected need equality by identity:

```csharp
record Proc(string Pid, string Cpu, string Mb, string Command)
{
    public virtual bool Equals(Proc? other) => other?.Pid == Pid;
    public override int GetHashCode() => Pid.GetHashCode();
}
```

## Streaming a long-running command (pinger.cs)

```csharp
CancellationTokenSource? stop = null;

async void Ping()
{
    stop = new CancellationTokenSource();
    running.Value = true;
    log.Value = "";
    try
    {
        await foreach (var line in Session.Default.Scoped(stop.Token).Sh($"ping -c 20 {host.Value}").StreamAsync())
            log.Value += line + "\n";
    }
    catch (OperationCanceledException) { log.Value += "— stopped —\n"; }
    catch (Exception error) { log.Value += error.Message + "\n"; }
    finally { running.Value = false; }
}

new Button().Content("Stop").OnClick(() => stop?.Cancel()).Bind(UIElement.IsEnabledProperty, running)
new ScrollViewer().Content(new TextBlock().Top().BindText(log).FontFamily(Platform.Mono).FontSize(11))
```

Cancelling kills the process. `StreamAsync` yields stdout lines and drains stderr. For a very chatty tool, keep only the last N lines instead of growing one string forever.

## Pipes

```csharp
var result = await (Sh($"git log --format=%an") | Sh($"sort") | Sh($"uniq -c")).RunAsync();
```

Usually it is simpler to do the second half in C# with LINQ over `r.Lines()`.

## Several values in one argument slot

```csharp
string[] files = ["a b.txt", "c.txt"];
Sh($"git add -- {files}")   // git add -- "a b.txt" c.txt
```
