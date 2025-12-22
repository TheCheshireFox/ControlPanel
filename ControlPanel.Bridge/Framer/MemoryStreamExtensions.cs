namespace ControlPanel.Bridge.Framer;

public static class MemoryStreamExtensions
{
    public static void Resize(this MemoryStream ms, int size)
    {
        if (size > ms.Length)
        {
            ms.SetLength(size);
            return;
        }
        
        Buffer.BlockCopy(ms.GetBuffer(), size, ms.GetBuffer(), 0, size);
        ms.SetLength(size);
    }
}