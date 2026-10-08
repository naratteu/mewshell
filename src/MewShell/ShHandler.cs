using System.Runtime.CompilerServices;
using System.Text;

namespace MewShell;

/// <summary>
/// Literal text splits on whitespace into arguments; an interpolated value is always exactly one
/// argument (or one per item, for a sequence). Nothing is ever handed to a shell to re-parse.
/// </summary>
[InterpolatedStringHandler]
public ref struct ShHandler
{
    private readonly List<string> _argv;
    private StringBuilder? _token;

    public ShHandler(int literalLength, int formattedCount)
    {
        _argv = new List<string>(formattedCount + 4);
        _token = null;
    }

    public void AppendLiteral(string value)
    {
        foreach (var c in value)
        {
            if (char.IsWhiteSpace(c)) Flush();
            else (_token ??= new StringBuilder()).Append(c);
        }
    }

    public void AppendFormatted<T>(T value)
    {
        if (value is not string && value is IEnumerable<string> many)
        {
            Flush();
            _argv.AddRange(many);
            return;
        }

        (_token ??= new StringBuilder()).Append(value?.ToString());
    }

    internal List<string> Argv()
    {
        Flush();
        return _argv;
    }

    private void Flush()
    {
        if (_token is null) return;
        _argv.Add(_token.ToString());
        _token = null;
    }
}
