# Reading a tool before wrapping it

A good GUI over a command line rests on output that is easy to parse and on commands that never stop to ask a question. Find both before writing any UI.

## 1. Ask the tool

```
<tool> --help
<tool> <subcommand> --help
man <tool>
```

Search the help for these flags:

| Look for | Why |
| --- | --- |
| `--json`, `--format`, `-o json`, `--output`, `--porcelain`, `--plain`, `-z` | Output meant for programs, which is stable across versions and locales |
| `--no-color`, `--color=never`, `--no-pager`, `--no-headers`, `--no-legend` | Removes decoration from the parsed text |
| `--yes`, `-y`, `--force`, `--no-input`, `--non-interactive`, `--batch` | Keeps it from waiting on a prompt that nobody can answer |
| `--limit`, `-n`, `--max-count` | Keeps lists small enough to show at once |
| `--quiet`, `-q` | Keeps only what the action returns |

Prefer, in this order:

1. JSON
2. a format template with tab separators
3. fixed, space-padded columns
4. free text

## 2. Run it the way the GUI will

The GUI reads the tool's output through a pipe, not a terminal. Many tools notice this. Some drop colors and progress bars, which is good. Others change their layout or buffer their output. See what the GUI will get:

```
<tool> <args> | cat
<tool> <args> < /dev/null | cat        # also no terminal on stdin: does it hang or prompt?
echo $?                                # what exit code means "nothing found"?
```

Try an empty result, a bad argument and a missing permission. The GUI must show each of them. A query puts stderr into `Error`.

## 3. Common tools

Check each against the installed version's `--help`; flags move between versions.

| Tool | Machine-readable form |
| --- | --- |
| git | `log --format=%h%x09%an%x09%s`, `for-each-ref --format=%(refname:short)`, `status --porcelain=v2 -z`, `show --numstat --format=` |
| gh | `--json field,field` on most list and view commands, plus `--jq` |
| docker / podman | `ps --format {{json .}}` (one object per line), `inspect` (a JSON array) |
| kubectl | `-o json`, `-o jsonpath=...`, `-o custom-columns=NAME:.metadata.name` |
| az | `-o json` or `-o tsv`, with `--query` (JMESPath) |
| aws | `--output json` or `--output text`, with `--query` |
| brew | `list --formula --versions`, `outdated --formula --verbose` (`name (old) < new`), `info --json=v2 <name>` (an object wrapping `formulae` and `casks`); set `HOMEBREW_NO_AUTO_UPDATE=1` so a read never updates taps |
| npm / pnpm | `ls --json`, `outdated --json` |
| ffprobe | `-v quiet -print_format json -show_format -show_streams <file>` |
| ps | `-axo pid=,pcpu=,rss=,comm=` (a trailing `=` drops the header) |
| systemctl | `list-units --no-pager --plain --no-legend` |
| dotnet | `list package --format json` |

## 4. Turn the help into a design

Write the design down before writing code:

- **Picks**: what the user chooses from a list or types. Each one becomes an `ObservableValue<T?>`.
- **Queries**: one command for each thing to show, each with the picks it depends on. A query takes at most two inputs directly.
- **Actions**: commands that change something, such as fetch, kill, install or delete. Each one is a button. After it runs, refresh the queries whose output it changed.
- **Streams**: commands that keep printing until stopped, such as logs, `ping`, `tail -f`, `docker logs -f` or `kubectl logs -f`.

Then sketch the chain:

```
repos ─pick→ repo ─┐
search box ────────┴→ issues ─pick→ issue ─→ body
```

Each arrow is a `Bind` or a `BindText`. Each box is a `Query` or an `ObservableValue`.
