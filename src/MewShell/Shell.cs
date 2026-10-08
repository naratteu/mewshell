namespace MewShell;

/// <summary><c>using static MewShell.Shell;</c> then <c>await Sh($"...")</c>: the default session, nothing more.</summary>
public static class Shell
{
    public static ShCommand Sh(ShHandler command) => Session.Default.Sh(command);
}
