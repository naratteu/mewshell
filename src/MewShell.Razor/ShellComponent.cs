using Naratteu.MewUI.Razor;

namespace MewShell.Razor;

/// <summary>What a command produced, as of this render.</summary>
public sealed record Output<T>(T? Value, bool Running, string? Error)
{
    /// <summary>Runs the same command line again.</summary>
    public Action Refresh { get; init; } = static () => { };
}

/// <summary>Hooks for components that sit on a command line.</summary>
public abstract class ShellComponent : HookComponent
{
    /// <summary>
    /// The parsed output of <paramref name="command"/>, run in <paramref name="session"/>. The command
    /// line is the dependency: when the values interpolated into it change, it runs again, and the
    /// run it replaces is cancelled, which kills its process. A null command runs nothing.
    /// </summary>
    protected Output<T> UseCommand<T>(Session session, ShCommand? command, Func<ShResult, T> parse)
    {
        var target = command?.On(session.NoThrow());
        var (output, setOutput) = UseState(new Output<T>(default, false, null));
        var (generation, setGeneration) = UseState(0);

        UseEffect(() =>
        {
            if (target is null)
            {
                setOutput(new Output<T>(default, false, null));
                return null;
            }

            var cancel = CancellationTokenSource.CreateLinkedTokenSource(session.Token);
            setOutput(output with { Running = true });
            _ = RunAsync();
            return cancel.Cancel;

            async Task RunAsync()
            {
                try
                {
                    var result = await target.RunAsync(cancel.Token);
                    setOutput(result.ExitCode == 0
                        ? new Output<T>(parse(result), false, null)
                        : new Output<T>(default, false, result.Stderr.Trim()));
                }
                catch (OperationCanceledException) when (cancel.IsCancellationRequested)
                {
                }
                catch (Exception exception)
                {
                    setOutput(new Output<T>(default, false, exception.Message));
                }
            }
        }, target?.Key, generation);

        return output with { Refresh = () => setGeneration(generation + 1) };
    }
}
