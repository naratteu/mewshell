# Running and publishing

## Run

```
dotnet run <name>.cs [args]
```

The first run restores the packages and compiles, which takes a few seconds. Later runs start at once. `args` are the words after the file name.

## Publish one executable

```
dotnet publish <name>.cs
```

A file-based app publishes with NativeAOT for the current OS and CPU by default, so no flags are needed. The result is one file:

| OS | Output |
| --- | --- |
| Windows | `artifacts/<name>/<name>.exe` |
| macOS | `artifacts/<name>/<name>` |
| Linux | `artifacts/<name>/<name>` |

Next to it you may find a `.dSYM` folder (macOS) or a `.dbg` file (Linux). These are debug symbols. Do not ship them.

Expect 5 to 7 MB per executable. The gallery apps, measured:

| Example | macOS arm64 | Linux arm64 |
| --- | --- | --- |
| gitview.cs | 5.4 MB | 6.3 MB |
| pinger.cs | 5.3 MB | 6.2 MB |
| procs.cs | 5.6 MB | 6.5 MB |
| issues.cs (with JSON) | 6.0 MB | 6.9 MB |
| brewview.cs (with JSON) | 6.0 MB | |

The publish must finish with no `IL2xxx` or `IL3xxx` warnings. A warning means some code will break or be trimmed away in the executable. It most often comes from reflection-based JSON; see the JSON pattern.

## One executable per OS

NativeAOT does not cross-compile between operating systems. Publish on each OS you want an executable for, or in CI with one job per OS. The same `.cs` file works everywhere because of `UseThisOS()`.

| OS | To publish | To run the executable |
| --- | --- | --- |
| macOS | .NET 10 SDK and Xcode command line tools | nothing |
| Linux | .NET 10 SDK, `clang` and `zlib1g-dev` | an X11 session with `libx11-6`, `libgl1`, Mesa drivers and `libfontconfig1`, plus fonts; install CJK fonts such as `fonts-noto-cjk` to show Korean, Japanese or Chinese |
| Windows | .NET 10 SDK and the Visual Studio "Desktop development with C++" workload | nothing |

Verified so far:

- macOS arm64: run and published.
- Linux arm64: run and published, under Xvfb in the `mcr.microsoft.com/dotnet/sdk:10.0` image.
- Windows: the same file compiles. Running it has not been verified by the skill authors. If something fails there, say so instead of guessing.

The commands a GUI runs must exist on the user's machine. A GUI over `ps` or `ping -c` is for macOS and Linux. Windows tools differ (`tasklist`, `ping -n`). If one file should serve every OS, pick the command per OS:

```csharp
var count = OperatingSystem.IsWindows() ? "-n" : "-c";
Sh($"ping {count} 20 {host}")
```

## Shipping

- The executable is self-contained and needs no .NET install.
- An executable downloaded on macOS is quarantined until the user allows it in System Settings, or until it is signed and notarized. For your own use: `xattr -d com.apple.quarantine <name>`.
- The window title comes from `.Title(...)`. The executable's name comes from the file name.
