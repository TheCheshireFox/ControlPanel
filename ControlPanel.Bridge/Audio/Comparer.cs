using System.Diagnostics.CodeAnalysis;

namespace ControlPanel.Bridge.Audio;

public class Comparer
{
    private static class ValueComparer<T>
    {
        public static IEqualityComparer<T> Instance = EqualityComparer<T>.Default;
    }

    private class DelegateEqualityComparer<T>(Func<T?, T?, bool> comparer) : IEqualityComparer<T>
    {
        public bool Equals(T? x, T? y) => comparer(x, y);
        public int GetHashCode([DisallowNull] T obj) => obj.GetHashCode();
    }
    
    public Comparer WithEqualityComparer<T>(Func<T?, T?, bool> comparer)
    {
        ValueComparer<T>.Instance = new DelegateEqualityComparer<T>(comparer);
        return this;
    }
    
    public bool IsEquals<T>(T x, T y) => ValueComparer<T>.Instance.Equals(x, y);
}