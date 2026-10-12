---
name: mewshell
description: Wrap any command-line tool in an interactive desktop GUI written as one short, readable C# file (a .NET 10 file-based app), then publish it as one small native executable. Use when someone wants a window, picker, table, dashboard, log viewer or front end over a CLI (git, gh, docker, kubectl, az, brew, ffmpeg, ps, systemctl, their own scripts, ...), or asks to turn a command line into an app. Built on MewShell (command lines whose interpolated values are their inputs) and MewUI. Not for web UIs, and not for changing MewShell or MewUI themselves.
license: VibeCoded AI-Slop License v1.0
---

# A GUI over a command line, in one file

The deliverable is a single `<name>.cs` of roughly 30 to 100 lines that:

- runs with `dotnet run <name>.cs`,
- publishes with `dotnet publish <name>.cs` to one NativeAOT executable of about 5 to 7 MB, with no runtime to install,
- reads top to bottom as: what the user picks, which command each pick feeds, where each command's output is shown.

Build only on public NuGet packages. No project file, no second source file.

## The model

```csharp
var branch = new ObservableValue<string?>("main");                       // what the user picks
var commits = git.Query(branch, b => Sh($"git log -n 300 {b}"), parse);  // a command fed by it
new ListBox().Bind(commits, commit, c => c.Subject)                      // its output, and the next pick
```

- `Sh($"...")` is a command line. Literal text splits on spaces into arguments. Each `{hole}` is exactly one argument, passed as is. Nothing is re-split, globbed or quoted, so a value can never inject anything. An array or list hole becomes one argument per item. No shell is involved.
- Anything the user picks or types is an `ObservableValue<T?>`.
- `session.Query(input, x => Sh(...), parse)` is a command's output, kept current. It runs at once and again whenever an input changes, and it kills the run still in flight first. `.Refresh()` runs it again. `Value`, `Running` and `Error` are observable.
- `control.Bind(query, picked, text)` puts the rows into a `ListBox`, `ComboBox` or `GridView` and the user's choice into `picked`. `picked` can be the input of the next query. That edge, from one command's output to the next command's argument, is the whole design.
- An action is an `async void` local function: it awaits `session.Sh(...).RunAsync()`, reports the result, and calls `.Refresh()` on the queries it changed.

## Workflow

1. **Check the ground.**
   - `dotnet --version` must be 10.0 or later; if it is older, stop and say so.
   - The tool must be installed: run `<tool> --version`.
   - Note the OS. Each published executable is for the OS it was published on.
2. **Study the tool** with [reading-a-tool.md](references/reading-a-tool.md).
   - Find its machine-readable output (`--json`, `--format`, `-o`, `--porcelain`) and its non-interactive flags.
   - Run the exact command lines you plan to use in the terminal, piped through `cat`. That is how the GUI will see them.
3. **Design before writing.** List these, then draw the chain: query → control → pick → next query.
   - the picks (inputs)
   - the queries (command → rows)
   - the actions (buttons)
   - any long-running streams
4. **Start from the closest app in the gallery.** Copy it to `<name>.cs` in the user's folder.
   - [gitview.cs](gallery/gitview.cs): list → detail chain from tab-separated output, a dropdown with a preset value, and an action button.
   - [issues.cs](gallery/issues.cs): JSON output, a search box as an argument, two inputs into one query, and loading and error lines.
   - [procs.cs](gallery/procs.cs): polling on a timer, a filter box, a grid, and an action on the selected row.
   - [pinger.cs](gallery/pinger.cs): streaming a long-running command line by line, with Stop.
   - [brewview.cs](gallery/brewview.cs): a check box that switches the command, JSON wrapped in an object, a details pane, and a program that may be missing from `PATH`.
   - More shapes are in [patterns.md](references/patterns.md).
5. **Compile, then run and exercise it.** `dotnet build <name>.cs` is the quick compile check. Then run it with `dotnet run <name>.cs [args]`.
   - Pick things, type, and press every button.
   - Turn on the command log while testing (`Session.Default with { Log = Console.WriteLine }`) to confirm the exact command lines.
   - If you can take screenshots, look at the window. Otherwise, ask the user to.
6. **Publish**: `dotnet publish <name>.cs` writes `artifacts/<name>/<name>` (`.exe` on Windows). Run that executable too. See [publish.md](references/publish.md).
7. **Report** these:
   - the file
   - how to run it
   - how to publish it
   - the executable's size
   - what you actually exercised
   - which OS you verified it on

## Rules

Each rule comes from a real failure. [troubleshooting.md](references/troubleshooting.md) has the symptoms.

- **Never name the file after the tool it runs.** If `ping.cs` runs `ping`, the app starts *itself*, because .NET looks for a program in the app's own folder before `PATH`. Use `pinger.cs`, `gitview.cs`, `procs.cs`.
- **One file holds everything.** A file-based app compiles only its entry file. Top-level statements come first; records, the JSON context and helpers go below them in the same file.
- **Pin the packages** to the versions in the template header, and keep every `Aprillz.MewUI*` package on one version.
  ```
  #:package Aprillz.MewUI@0.22.1
  #:package Naratteu.MewShell.MewUI@0.1.0-preview.1
  ```
- **Copy the `Platform` block** from any gallery app unchanged. `UseThisOS()` makes one file run on Windows, macOS and Linux. `Platform.Mono` names a monospace font that exists on each OS.
- **There is no shell.** Instead of shell syntax, use C#:
  - Pipes: compose commands with `|`, as in `Sh($"a") | Sh($"b")`.
  - Globs: `Directory.GetFiles`.
  - `~`: `Environment.GetFolderPath`.
  - `$VAR`: `.With(name, value)` or `Environment.GetEnvironmentVariable`.
  - `&&`: two awaits.
  - Redirection: `ShResult.Stdout`.
- **Braces in a format string** are C# interpolation braces. Put such a value in a hole: `var fmt = "{{json .}}"; Sh($"docker ps --format {fmt}")`.
- **Query inputs are reference types**, such as `string?` or a record. A query takes up to two inputs. For a `bool` toggle, or for more than two inputs, see [patterns.md](references/patterns.md#a-toggle-or-more-than-two-inputs).
- **A query's parse may read other values' `.Value`**, such as a filter applied to the rows. Every value the query depends on must call `Refresh` when it changes, either as an input or through `Changed += q.Refresh`.
- **Rows must keep their identity across refreshes.** Override a record's `Equals` and `GetHashCode` to compare its key, as `Proc` does by PID. Otherwise a refresh that changes a number in the row clears the user's selection.
- **Parse into `IReadOnlyList<T>`.**
  - For JSON, use a source-generated `JsonSerializerContext` in the same file. Reflection-based JSON breaks NativeAOT.
  - Cast the parsed array: `(IReadOnlyList<Repo>)JsonSerializer.Deserialize(r.Stdout, Json.Default.RepoArray)!`.
- **Nothing may prompt.** The tool's stdin is not a terminal, so a prompt waits forever.
  - Pass `--yes`, `--no-input`, `--non-interactive` or `--force`, whichever the tool takes.
  - Set `GIT_TERMINAL_PROMPT=0` and `NO_COLOR=1` with `.With(...)`.
- **Show the waiting and the failures.**
  - Bind `query.Running` to a "Loading…" line for anything that touches the network.
  - Bind `query.Error`, which holds stderr of a failed run, to a line the user can see. Give that line its own row, let it wrap, and show it only when it is set, because errors are long. A line in a toolbar gets clipped.
  - For actions, use `.NoThrow()` and check `ExitCode`. Exit code 1 means "nothing found" for `grep` and `pgrep`, not failure.
- **Put text in a ScrollViewer with `.Top()`.** Without it, short output sits in the vertical middle.
- **Name tools that live outside the system folders by their full path when it exists.** An app opened from Finder or a desktop menu gets a shorter `PATH` than the terminal. A hole may be the program: `var exe = File.Exists("/opt/homebrew/bin/brew") ? "/opt/homebrew/bin/brew" : "brew"; Sh($"{exe} list")`.

## Layout in a breath

- Window: `new Window().Title(..).Resizable(w, h).Content(...)`.
- Toolbar and body: `new DockPanel().Spacing(8).Margin(16).Children(toolbar.DockTop(), body)`.
- A row of controls: `new StackPanel().Horizontal().Spacing(8).Children(...)`.
- A grid:
  - `new Grid().Columns("440,*").Rows("Auto,*,2*").Spacing(10)`
  - place children with `.Row(n)`, `.Column(n)`, `.RowSpan(n)` and `.ColumnSpan(n)`.
- Controls:
  - `TextBox().Placeholder("Filter").BindText(value)`
  - `CheckBox().Content("Outdated only").BindIsChecked(flag)`, with `flag` an `ObservableValue<bool>`
  - `Button().Content("Go").OnClick(Go)`
  - `TextBlock().BindText(value, v => ...)`
  - `ScrollViewer().Content(...)`
- Grid columns: `GridViewColumn<T>().Header("Name").PixelWidth(80)` or `.StarWidth()`, each with `.Text(row => ...)`.
- Enabled state: `.Bind(UIElement.IsEnabledProperty, picked, p => p is not null)`.
- Shown only when set: `.BindIsVisible(query.Error, e => e is not null)`.

For any MewUI control or layout question beyond this, use the `mewui` skill (`aprillz/mewui-skill`) if it is installed.
