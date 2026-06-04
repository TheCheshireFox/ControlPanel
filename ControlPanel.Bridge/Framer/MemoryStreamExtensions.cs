namespace ControlPanel.Bridge.Framer;

public static class MemoryStreamExtensions
{
    public static void DiscardPrefix(this MemoryStream ms, int prefixLength)
    {
        if (prefixLength <= 0)
            return;

        if (prefixLength >= ms.Length)
        {
            ms.SetLength(0);
            return;
        }

        var remaining = (int)ms.Length - prefixLength;
        Buffer.BlockCopy(ms.GetBuffer(), prefixLength, ms.GetBuffer(), 0, remaining);
        ms.SetLength(remaining);
    }
}
