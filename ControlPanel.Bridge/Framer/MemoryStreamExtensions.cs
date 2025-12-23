namespace ControlPanel.Bridge.Framer;

public static class MemoryStreamExtensions
{
    public static void ShrinkTo(this MemoryStream ms, int size)
    {
        if (size > ms.Length)
            return;
        
        Buffer.BlockCopy(ms.GetBuffer(), size, ms.GetBuffer(), 0, size);
        ms.SetLength(size);
    }
}