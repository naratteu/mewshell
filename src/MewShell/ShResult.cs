namespace MewShell;

/// <summary>What a finished command left behind.</summary>
public sealed record ShResult(int ExitCode, string Stdout, string Stderr)
{
    public string Text() => Stdout.TrimEnd('\n', '\r');

    public IReadOnlyList<string> Lines() => Text().Split('\n', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>Each line split into fields: the shape of <c>git log --format=%h%x09%s</c> and its kin.</summary>
    public IEnumerable<string[]> Fields(char separator = '\t') => Lines().Select(line => line.Split(separator));
}
