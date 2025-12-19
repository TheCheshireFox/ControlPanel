namespace ControlPanel.Bridge.Framer;

internal class MemoryBlock
{
    private readonly Memory<byte> _memory;
        
    public int Count { get; private set; }
    public ReadOnlySpan<byte> Span => _memory.Span;
    public bool IsFull => _memory.Length == Count;
    public int MaxSize => _memory.Length;
        
    public MemoryBlock(int size) => _memory = new byte[size];

    public void Add(ReadOnlySpan<byte> value)
    {
        if (Count + value.Length > _memory.Length)
            throw new Exception("Memory is full.");
            
        value.CopyTo(_memory[Count..].Span);
        Count += value.Length;
    }
        
    public void Add(byte value) => Add([value]);

    public void Clear() => Count = 0;
}