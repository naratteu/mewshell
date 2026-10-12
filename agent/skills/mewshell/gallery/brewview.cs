#:package Aprillz.MewUI@0.22.1
#:package Naratteu.MewShell.MewUI@0.1.0-preview.1

// Installed Homebrew formulae (filter, outdated only) → details of the picked one. Run: dotnet run brewview.cs

using System.Text.Json;
using System.Text.Json.Serialization;
using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using MewShell;
using MewShell.MewUI;
using static MewShell.Shell;

// Read-only brew commands; never let one update the taps or print hints and colors.
// An app opened from Finder does not get the terminal's PATH, so name brew by its full path when it is there.
var exe = File.Exists("/opt/homebrew/bin/brew") ? "/opt/homebrew/bin/brew" : "brew";
var brew = Session.Default.With("HOMEBREW_NO_AUTO_UPDATE", "1").With("HOMEBREW_NO_ENV_HINTS", "1").With("NO_COLOR", "1");

// What the user types, toggles and picks.
var filter = new ObservableValue<string?>("");
var outdatedOnly = new ObservableValue<bool>(false);
var formula = new ObservableValue<Formula?>(null);

// The list: every installed formula, or only the outdated ones. The filter applies to the names.
var formulae = brew.Query(
    () => outdatedOnly.Value ? Sh($"{exe} outdated --formula --verbose") : Sh($"{exe} list --formula --versions"),
    r => Formula.Parse(r, filter.Value ?? ""));
filter.Changed += formulae.Refresh;
outdatedOnly.Changed += formulae.Refresh;

// The details of the picked formula.
var info = brew.Query(formula, f => Sh($"{exe} info --json=v2 {f.Name}"),
    r => JsonSerializer.Deserialize(r.Stdout, Json.Default.InfoResult)!.Formulae.FirstOrDefault());

Application.Create().UseThisOS()
    .BuildMainWindow(() => new Window().Title("Homebrew formulae").Resizable(1000, 640).Content(
        new DockPanel().Spacing(10).Margin(16).Children(
            new StackPanel().Horizontal().Spacing(8).DockTop().Children(
                new TextBox().Width(260).Placeholder("Filter by name").BindText(filter!),
                new CheckBox().Content("Outdated only").BindIsChecked(outdatedOnly).CenterVertical(),
                new TextBlock().BindText(formulae.Value, l => l is null ? "" : $"{l.Count} formulae").CenterVertical()),
            new TextBlock().DockTop().BindText(formulae.Error, e => e ?? "").BindIsVisible(formulae.Error, e => e is not null).TextWrapping(TextWrapping.Wrap),
            new Grid().Columns("340,*").Spacing(10).Children(
                new ListBox().Bind(formulae, formula, f => f.Latest is null ? $"{f.Name}  {f.Installed}" : $"{f.Name}  {f.Installed} → {f.Latest}"),
                new ScrollViewer().Column(1).Content(
                    new StackPanel().Top().Spacing(6).Children(
                        new TextBlock().BindText(info.Running, busy => busy ? "Loading…" : ""),
                        new TextBlock().BindText(info.Error, e => e ?? ""),
                        new TextBlock().BindText(info.Value, i => i?.Name ?? "Pick a formula").FontSize(20),
                        new TextBlock().BindText(info.Value, i => i?.Desc ?? "").TextWrapping(TextWrapping.Wrap),
                        new TextBlock().BindText(info.Value, Details).FontFamily(Platform.Mono).FontSize(12).TextWrapping(TextWrapping.Wrap)))))))
    .Run();

static string Details(Info? i) => i is null ? "" : string.Join("\n", (string[])[
    $"Installed:    {string.Join(", ", i.Installed.Select(v => v.Version))}",
    $"Latest:       {i.Versions.Stable}{(i.Revision > 0 ? $"_{i.Revision}" : "")}{(i.Outdated ? "  (outdated)" : "")}",
    $"Homepage:     {i.Homepage}",
    $"License:      {i.License ?? "—"}",
    $"Dependencies: {(i.Dependencies.Length == 0 ? "none" : "")}",
    .. i.Dependencies.Select(d => $"  {d}")]);

record Formula(string Name, string Installed, string? Latest)
{
    // The same formula across refreshes keeps the selection.
    public virtual bool Equals(Formula? other) => other?.Name == Name;
    public override int GetHashCode() => Name.GetHashCode();

    // "name 1.2 1.3" from brew list, or "name (1.2) < 1.3" from brew outdated --verbose.
    public static IReadOnlyList<Formula> Parse(ShResult r, string filter) =>
    [
        .. r.Lines()
            .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(f => f.Length >= 2 && f[0].Contains(filter, StringComparison.OrdinalIgnoreCase))
            .Select(f => f.Contains("<")
                ? new Formula(f[0], string.Join(" ", f[1..Array.IndexOf(f, "<")]).Trim('(', ')'), f[^1])
                : new Formula(f[0], string.Join(" ", f[1..]), null)),
    ];
}

record InfoResult(Info[] Formulae);
record Info(string Name, string? Desc, string? Homepage, string? License, Versions Versions, Installed[] Installed, string[] Dependencies, int Revision, bool Outdated);
record Versions(string? Stable);
record Installed(string Version);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(InfoResult))]
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
