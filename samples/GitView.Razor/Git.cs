using MewShell;

namespace GitView.Razor;

public sealed record Commit(string Hash, string Date, string Author, string Subject)
{
    public static IReadOnlyList<Commit> Parse(ShResult log) =>
        [.. log.Fields().Select(f => new Commit(f[0], f[1], f[2], f[3]))];
}

public sealed record Change(string Added, string Deleted, string Path)
{
    public static IReadOnlyList<Change> Parse(ShResult numstat) =>
        [.. numstat.Fields().Select(f => new Change(f[0], f[1], f[2]))];
}
