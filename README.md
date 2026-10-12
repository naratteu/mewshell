# MewShell

[한국어](README.ko.md)

Command lines for [MewUI](https://github.com/aprillz/MewUI) desktop apps: written as interpolated
strings, bound to lists, drop-downs and grids, and run again when what they depend on changes.

```razor
var (branch, setBranch) = UseState<string?>("main");
var (commit, setCommit) = UseState<Commit?>(null);

var branches = UseCommand(git, Sh($"git for-each-ref --format=%(refname:short) refs/heads"), r => r.Lines());
var commits  = UseCommand(git, branch is null ? null : Sh($"git log -n 300 --format=... {branch}"), Commit.Parse);
var changes  = UseCommand(git, commit is null ? null : Sh($"git show --numstat --format= {commit.Hash}"), Change.Parse);
```
```razor
<MewDropdown TItem="string" Items="branches.Value" Selected="branch" SelectedChanged="setBranch" />
<MewList TItem="Commit" Items="commits.Value" Selected="commit" SelectedChanged="setCommit" />
<MewTable TItem="Change" Items="changes.Value" Columns="ChangeColumns" />
```

Picking a branch runs `git log` for it; picking a commit runs `git show` for that one. The value a
user picks goes into the next command line as an argument, and the command line is what decides
when to run again. [`samples/GitView.Razor`](samples/GitView.Razor) is this, whole, and
[`samples/GitView.MewUI`](samples/GitView.MewUI) is the same app in plain MewUI.

## Install

```bash
dotnet add package Naratteu.MewShell --prerelease         # command lines and sessions
dotnet add package Naratteu.MewShell.MewUI --prerelease   # Query and Bind, for MewUI
dotnet add package Naratteu.MewShell.Razor --prerelease   # UseCommand and typed selectors, for MewRazor
```

The UI packages bring the core with them, and `MewShell.Razor` brings
[MewRazor](https://www.nuget.org/packages/Naratteu.MewUI.Razor) along with its MEW001 analyzer.

To have an agent write a whole GUI as one file and publish it as one executable, see the
[gallery](#gallery) and the [agent skill](#agent-skill).

## Command lines as interpolated strings

```csharp
using static MewShell.Shell;

await Sh($"git commit -m {message}");
var head = (await Sh($"git rev-parse HEAD")).Text();
var four = (await (Sh($"seq 1 5") | Sh($"tail -n 2"))).Lines();
```

Literal text splits on whitespace into arguments. An interpolated value never splits: it is one
argument, whatever is in it. Nothing passes through a shell, so there is nothing to quote and
nothing to inject:

```csharp
var evil = "a b; rm -rf / $(whoami)";
await Sh($"printf %s {evil}");          // printf receives exactly two arguments
```

A value written against literal text joins its token (`--author={name}`), and a sequence becomes
one argument per item (`git add {files}`). A result offers `Text()`, `Lines()` and `Fields()`;
`StreamAsync()` yields lines while the process is still running. A non-zero exit throws unless the
session says `NoThrow()`.

## Sessions

```csharp
var git = Session.Default.In(repository).Scoped(windowClosed.Token) with { Log = history.Add };
```

A session is where commands run and what they inherit: a directory, an environment, whether
failure throws, a cancellation token, and a log that sees each command line. It is an immutable
value. A GUI usually has several things running at once, and a global `cd` — Bun Shell's `$.cwd()`
— would let one window change another's directory; deriving a session cannot. Cancelling a
session's token kills every process started from it, so closing a window takes its commands with
it.

## Binding to a UI

### MewUI — `MewShell.MewUI`

```csharp
_commits = _git.Query(_branch, branch => Sh($"git log -n 300 {branch}"), Commit.Parse);
_changes = _git.Query(_commit, commit => Sh($"git show --numstat --format= {commit.Hash}"), Change.Parse);

new ListBox().Bind(_commits, _commit, c => $"{c.Hash}  {c.Subject}")
new GridView().Bind(_changes, _change).Columns(...)
```

A `Query` is a command's output kept current. It follows the `ObservableValue`s it is given, runs
again when one changes, and cancels the run it replaces. `Bind` puts its rows into a `ListBox`,
`ComboBox` or `GridView` and the picked row into a value — which is the input of the next query.
When the picked row is not among new rows, the value is cleared, and everything downstream with
it.

### MewRazor — `MewShell.Razor`

Inherit `ShellComponent` for `UseCommand`, and use `MewList`, `MewDropdown` and `MewTable` for
typed selection: `Items` in, `Selected` and `SelectedChanged` out, the way a controlled input works
in React. They keep every parameter of the MewUI control they wrap, so `Width`, `Margin`, `Row` and
`Column` all work.

## How the values become the dependencies

`Sh($"git log {branch}")` is not string formatting. The C# compiler hands it to an
[interpolated string handler](src/MewShell/ShHandler.cs) piece by piece, so the handler sees which
parts were literal text and which were values, and keeps each value as an argument of its own. The
result is an argument list, and a command's `Key` is that list together with its directory and
environment.

`UseCommand` gives that key to `UseEffect` as the dependency. When `branch` changes, the argument
list changes, the key changes, and the effect runs: its cleanup cancels the previous run — which
kills the process — and the new one starts. When the values are the same, the key is the same and
nothing runs. A null command runs nothing, which is how "no commit picked yet" is written.

There is no tracking and no list of dependencies to keep in step with the command; the command line
already says what it depends on. MewUI has no render loop to re-evaluate a lambda, so there a
`Query` takes its inputs explicitly instead.

## Gallery

**[Every app with its full source under the screenshot →](docs/gallery/README.md)**

Each app is one `.cs` file. `dotnet run <file>.cs` runs it, and `dotnet publish <file>.cs` turns it
into one NativeAOT executable with no .NET to install. Line counts leave out comments, blank lines
and the 10-line block that picks the platform for each OS.

| | |
| --- | --- |
| ![gitview](docs/gallery/gitview.png) | **[gitview.cs](docs/gallery/README.md#gitviewcs)**: 47 lines, 5.4 MB<br>Branches → commits → changed files → diff, plus Fetch. Each pick is an argument of the next `git` command. |
| ![issues](docs/gallery/issues.png) | **[issues.cs](docs/gallery/README.md#issuescs)**: 38 lines, 6.0 MB<br>Repositories → issues → body, from `gh --json`. A search box and the picked repository feed one query. |
| ![procs](docs/gallery/procs.png) | **[procs.cs](docs/gallery/README.md#procscs)**: 47 lines, 5.6 MB<br>`ps` every two seconds, a filter box, and Kill on the selected row. The selection survives each refresh. |
| ![pinger](docs/gallery/pinger.png) | **[pinger.cs](docs/gallery/README.md#pingercs)**: 33 lines, 5.3 MB<br>A long-running command streamed line by line. Stop kills the process. |
| ![brewview](docs/gallery/brewview.png) | **[brewview.cs](docs/gallery/README.md#brewviewcs)**: 67 lines, 6.0 MB<br>Homebrew formulae with an "Outdated only" check box and a details pane. A fresh agent wrote it from [the skill](#agent-skill) alone, as a test. |

Sizes are for macOS arm64; Linux arm64 executables come out about 1 MB larger. The same files run on
Linux, tested under Xvfb. They compile for Windows, but have not been run there yet.

## Agent skill

[`agent/skills/mewshell`](agent/skills/mewshell) teaches a coding agent to do the above for any
command-line tool:

1. read the tool's help,
2. pick its machine-readable output,
3. write one file from the gallery,
4. run and exercise it,
5. publish the executable,
6. report what it checked.

Install it with one of these:

```text
/plugin marketplace add naratteu/mewshell        # Claude Code, then:
/plugin install mewshell@naratteu

copilot plugin marketplace add naratteu/mewshell # GitHub Copilot, then:
copilot plugin install mewshell@naratteu

gh skill install naratteu/mewshell mewshell --agent claude-code --scope user   # any agent gh supports
```

In Codex, run `$skill-installer` and ask for the `mewshell` skill from `naratteu/mewshell`. Or copy
the folder into `.claude/skills/`, `.agents/skills/` or `.github/skills/`. Then ask for a GUI:

> Make me a GUI for docker: running containers in a table, their logs streaming below when I pick
> one, and Stop and Restart buttons. Publish it as one executable.

## Samples

| | What it shows | Run |
| --- | --- | --- |
| [`GitView.Razor`](samples/GitView.Razor), [`GitView.MewUI`](samples/GitView.MewUI) | Drop-down → list → grid → diff, each pick an argument of the next `git` command; a Fetch button | `dotnet run --project samples/GitView.Razor -- <repository>` |
| [`Top.Razor`](samples/Top.Razor), [`Top.MewUI`](samples/Top.MewUI) | Polling every second and re-running on each keystroke, with R3 (`Drop`, `Switch`, `Debounce`) | `dotnet run --project samples/Top.Razor` |

The samples register the macOS platform; swap the two `Use…` lines for Windows or Linux as in
[MewRazor's table](https://github.com/naratteu/mewrazor#run-it-in-one-command). The Top samples run
`pgrep` and `sysctl`, which are macOS commands.

## NativeAOT

Every library is marked `IsAotCompatible`, and CI publishes [`eng/aottest`](eng/aottest) with
NativeAOT and runs it — real processes, pipes, sessions and cancellation, checked by value. That
binary is 2.9 MB; the git explorer samples publish to 5.3 MB (MewUI) and 6.1 MB (MewRazor).

## Not done yet

- **No terminal.** A program sees a pipe, not a terminal, so colours, prompts and pagers behave as
  they do when output is redirected. Most tools have flags for that (`--no-pager`, `--porcelain`,
  `NO_COLOR`); a pseudo-terminal is a separate piece of work.
- **No built-in commands.** Bun Shell ships `ls`, `cd`, `rm` and friends so scripts run on Windows
  too. Here a command is whatever is on the `PATH`.

## Built on

[CliWrap](https://github.com/Tyrrrz/CliWrap) runs the processes; [MewUI](https://github.com/aprillz/MewUI)
draws the windows; [MewRazor](https://github.com/naratteu/mewrazor) renders Razor components into
them.

## License

[VibeCoded AI-Slop License v1.0](LICENSE). This code was written by an AI from prompts, and the
license is honest about what that means.
