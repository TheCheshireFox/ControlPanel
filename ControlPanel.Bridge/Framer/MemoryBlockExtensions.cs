using System.Buffers.Binary;

namespace ControlPanel.Bridge.Framer;

internal static class MemoryBlockExtensions
{
    public static T As<T>(this MemoryBlock memoryBlock)
    {
        return Type.GetTypeCode(typeof(T)) switch
        {
            TypeCode.Byte => (T)Convert.ChangeType(memoryBlock.Span[0], TypeCode.Byte),
            TypeCode.UInt16 => (T)Convert.ChangeType(BinaryPrimitives.ReadUInt16BigEndian(memoryBlock.Span), TypeCode.UInt16),
            TypeCode.UInt32 => (T)Convert.ChangeType(BinaryPrimitives.ReadUInt32BigEndian(memoryBlock.Span), TypeCode.UInt32),
            _ => throw new NotSupportedException($"Not supported type {typeof(T).Name}")
        };
    }
}