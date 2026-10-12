#:package Aprillz.MewUI@0.22.1
#:package Naratteu.MewShell.MewUI@0.1.0-preview.1

// A long-running command streamed line by line, with Stop. Run: dotnet run pinger.cs

using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using MewShell;
using static MewShell.Shell;

var host = new ObservableValue<string>("127.0.0.1");
var log = new ObservableValue<string>("");
var running = new ObservableValue<bool>(false);
CancellationTokenSource? stop = null;

// A long-running command: lines are appended as they arrive, and Stop kills the process.
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

Application.Create().UseThisOS()
    .BuildMainWindow(() => new Window().Title("Ping").Resizable(640, 420).Content(
        new DockPanel().Spacing(8).Margin(16).Children(
            new StackPanel().Horizontal().Spacing(8).DockTop().Children(
                new TextBox().Width(260).BindText(host),
                new Button().Content("Ping").OnClick(Ping).Bind(UIElement.IsEnabledProperty, running, r => !r, r => !r),
                new Button().Content("Stop").OnClick(() => stop?.Cancel()).Bind(UIElement.IsEnabledProperty, running)),
            new ScrollViewer().Content(new TextBlock().Top().BindText(log).FontFamily(Platform.Mono).FontSize(11)))))
    .Run();

static class Platform
{
    // One file for every OS: register the host and renderer of the one it runs on.
    public static string Mono => OperatingSystem.IsWindows() ? "Consolas" : OperatingSystem.IsMacOS() ? "Menlo" : "monospace";

    public static ApplicationBuilder UseThisOS(this ApplicationBuilder app) =>
        OperatingSystem.IsWindows() ? app.UseWin32().UseDirect2D()
        : OperatingSystem.IsMacOS() ? app.UseMacOS().UseMewVGMetal()
        : app.UseX11().UseMewVGX11();
}
