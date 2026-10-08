using static MewShell.Shell;

namespace MewShell.Tests;

/// <summary>
/// A command's key is what a UI compares to decide whether to run it again. Because every
/// interpolated value is an argument, the values written into a command line are exactly its
/// dependencies: change one and the key changes, keep them and it does not.
/// </summary>
public class KeyTests
{
    private static ShCommand Log(string branch) => Sh($"git log -n 300 {branch}");

    [Fact]
    public void SameValuesSameKey()
    {
        Assert.Equal(Log("main").Key, Log("main").Key);
    }

    [Fact]
    public void ADifferentValueIsADifferentKey()
    {
        Assert.NotEqual(Log("main").Key, Log("dev").Key);
    }

    [Fact]
    public void AValueWithSpacesCannotCollideWithTwoArguments()
    {
        // "a b" as one argument and "a" "b" as two print the same, but they are different commands.
        string one = "a b";
        string[] two = ["a", "b"];

        Assert.NotEqual(Sh($"echo {one}").Key, Sh($"echo {two}").Key);
    }

    [Fact]
    public void TheSessionIsPartOfTheKey()
    {
        var command = Log("main");

        Assert.NotEqual(command.On(Session.Default.In("/")).Key, command.On(Session.Default.In("/usr")).Key);
        Assert.NotEqual(command.Key, command.On(Session.Default.With("GIT_DIR", "x")).Key);
    }
}
