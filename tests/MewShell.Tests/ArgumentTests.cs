using static MewShell.Shell;

namespace MewShell.Tests;

/// <summary>
/// Literal text splits on whitespace; an interpolated value never does. There is no shell, so
/// there is nothing to quote for, and a value cannot turn into more than the argument it was.
/// </summary>
public class ArgumentTests
{
    [Fact]
    public void LiteralTextSplitsIntoArguments()
    {
        Assert.Equal(["git", "log", "-n", "5"], Sh($"git log -n 5").Argv);
    }

    [Fact]
    public void AHostileValueStaysOneArgument()
    {
        var evil = "a b; rm -rf / && echo pwned $(whoami) `id` \"quoted\" 'single'";

        Assert.Equal(["printf", "%s", evil], Sh($"printf %s {evil}").Argv);
    }

    [Fact]
    public void AValueTouchingLiteralTextJoinsItsToken()
    {
        var name = "x y";

        Assert.Equal(["git", "log", "--author=x y"], Sh($"git log --author={name}").Argv);
    }

    [Fact]
    public void ASequenceSplatsIntoOneArgumentPerItem()
    {
        string[] files = ["a", "b c", "d"];

        Assert.Equal(["git", "add", "a", "b c", "d"], Sh($"git add {files}").Argv);
    }

    [Fact]
    public async Task TheArgumentsReachTheProgramUntouched()
    {
        var evil = "a b; rm -rf / $(whoami)";

        Assert.Equal(evil, (await Sh($"printf %s {evil}")).Stdout);
    }
}
