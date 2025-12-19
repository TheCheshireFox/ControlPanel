using System.Text;

namespace ControlPanel.Shared;

public class AliasEncodingProvider : EncodingProvider
{
    private readonly Dictionary<string, Encoding> _aliases;

    public AliasEncodingProvider(Dictionary<string, Encoding> aliases)
    {
        _aliases = aliases;
    }

    public override Encoding? GetEncoding(int codepage) => null;
    public override Encoding? GetEncoding(string name) => _aliases.GetValueOrDefault(name);
}