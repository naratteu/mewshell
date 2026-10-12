# Troubleshooting

Find the symptom, then apply the fix.

## The command starts but nothing comes back, and a second window may open

**Cause:** the file is named after the tool it runs, for example `ping.cs` running `ping`. .NET looks for a program in the app's own folder before `PATH`, so the app starts a copy of itself.

**Fix:** rename the file, for example to `pinger.cs`.

## "Failed to start a process with file path '...'"

**Cause:** the program is not installed, or it is not on the `PATH` this app sees. Apps launched from Finder or a desktop menu get a shorter `PATH` than your terminal, typically `/usr/bin:/bin:/usr/sbin:/sbin`. Homebrew's `/opt/homebrew/bin` is not on it.

**Fix:** check `<tool> --version`. A hole may be the program, so fall back to the full path:

```csharp
var exe = File.Exists("/opt/homebrew/bin/brew") ? "/opt/homebrew/bin/brew" : "brew";
Sh($"{exe} list --formula")
```

To reproduce it from a terminal: `env -i HOME=$HOME PATH=/usr/bin:/bin ./artifacts/<name>/<name>`. `Query.Error` shows this message.

## A query never finishes, or `Running` stays true

**Cause:** the tool is waiting for input, such as a confirmation, a password or a pager.

**Fix:**

- Add the tool's non-interactive flag (`--yes`, `--no-input`, `--no-pager`).
- Set `.With("GIT_TERMINAL_PROMPT", "0")` for git.
- Log in to the tool in a terminal first (`gh auth login`, `az login`).

## Garbage like `[32m` in the text

**Cause:** color codes.

**Fix:** pass `--no-color` or `--color=never`, or set `.With("NO_COLOR", "1")`.

## The parse throws, or `Error` says the input is not valid JSON

**Cause:** the tool printed a warning or a banner on stdout, or `--json` needs a field list (`gh ... --json name,title`).

**Fix:** run the exact command piped through `cat` and look at it.

## Cannot convert `T[]` or `List<T>` to `IReadOnlyList<T>` in a query's parse

**Cause:** `Query` and `Bind` work on `IReadOnlyList<T>`.

**Fix:** cast the parsed array, `(IReadOnlyList<Repo>)...!`, or build with a collection expression, `[.. ...]`.

## A query's input will not compile with `int` or `bool`

**Cause:** inputs must be reference types.

**Fix:** use `string?` or a record. Or use the overload with no inputs and refresh it on `Changed`; see "A toggle, or more than two inputs" in patterns.md.

## An error message is cut off at the window's edge

**Fix:** give it its own wrapping row; see "Loading and error lines" in patterns.md.

## The selection jumps away every time a polled list refreshes

**Cause:** the rows compare by all their fields, and a number in the row changed.

**Fix:** override `Equals` and `GetHashCode` to compare the key (see `Proc` in procs.cs).

## A record, JSON context or helper is "not found"

**Cause:** it is in a second file. A file-based app compiles only the entry file.

**Fix:** move it into `<name>.cs`, below the top-level statements.

## `{` or `}` in an argument disappears or fails to compile

**Cause:** in `$"..."`, braces start a hole.

**Fix:** put the value in a variable and use a hole: `var fmt = "{{json .}}"; Sh($"docker ps --format {fmt}")`.

## A `*.txt`, `~/x`, `$HOME`, `a && b` or `> file` argument does nothing useful

**Cause:** there is no shell, so these reach the program as literal text.

**Fix:** use C#: `Directory.GetFiles`, `Environment.GetFolderPath`, `.With(...)`, two awaits, `ShResult.Stdout`.

## Short output sits in the vertical middle of a pane

**Fix:** `new TextBlock().Top()` inside the `ScrollViewer`.

## Wrong font, or boxes instead of letters

- **Monospace:** `Menlo` exists only on macOS. Use `Platform.Mono`.
- **Boxes for Korean, Japanese or Chinese on Linux:** install a CJK font such as `fonts-noto-cjk`.

## Publish warns `IL2026`, `IL3050` or another `IL2xxx`/`IL3xxx`

**Cause:** usually reflection-based `JsonSerializer.Deserialize<T>(string)`.

**Fix:** use the source-generated context overload, `JsonSerializer.Deserialize(s, Json.Default.TypeName)`. Do not ship with warnings.

## The published executable will not open on another Mac

**Cause:** Gatekeeper quarantine.

**Fix:** see publish.md.

## It works on macOS but not on Windows

**Cause:** the commands themselves differ between OSes. The GUI code does not.

**Fix:**

- Pick the command per OS.
- On Windows, a tool installed as a `.cmd` or `.bat` file (npm, az, code) may need its extension, as in `npm.cmd`. Windows has not been verified by the skill authors, so report what you saw.
