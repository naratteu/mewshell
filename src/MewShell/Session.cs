using System.Collections.Immutable;
using CliWrap;

namespace MewShell;

/// <summary>
/// Where commands run and what they inherit. Immutable, so deriving one for a subdirectory or a
/// window cannot change what another task sees -- which a global "cd" would.
/// </summary>
public sealed record Session
{
    public static Session Default { get; } = new();

    public string? Cwd { get; init; }

    public ImmutableDictionary<string, string?> Env { get; init; } = ImmutableDictionary<string, string?>.Empty;

    public bool Throws { get; init; } = true;

    /// <summary>Cancelling it ends every command started from this session.</summary>
    public CancellationToken Token { get; init; }

    /// <summary>Sees each command line as it starts: a GUI's history pane.</summary>
    public Action<string>? Log { get; init; }

    public Session In(string path) => this with { Cwd = Path.GetFullPath(path, Cwd ?? Environment.CurrentDirectory) };

    public Session With(string name, string? value) => this with { Env = Env.SetItem(name, value) };

    public Session NoThrow() => this with { Throws = false };

    public Session Scoped(CancellationToken token) => this with { Token = token };

    public ShCommand Sh(ShHandler command) => new(command.Argv(), this);

    internal Command Build(IReadOnlyList<string> argv)
    {
        var cli = Cli.Wrap(argv[0])
            .WithArguments(argv.Skip(1))
            .WithEnvironmentVariables(Env)
            .WithValidation(Throws ? CommandResultValidation.ZeroExitCode : CommandResultValidation.None);

        return Cwd is null ? cli : cli.WithWorkingDirectory(Cwd);
    }
}
