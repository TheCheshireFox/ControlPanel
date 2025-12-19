namespace ControlPanel.Bridge.Extensions;

public static class DelegateExtensions
{
    public static async Task InvokeAllAsync<T1>(this Func<T1, Task>? func, T1 arg1)
    {
        if (func == null) return;
        foreach (var fn in func.GetInvocationList()) await ((Func<T1, Task>)fn)(arg1);
    }
    
    public static async Task InvokeAllAsync<T1, T2>(this Func<T1, T2, Task>? func, T1 arg1, T2 arg2)
    {
        if (func == null) return;
        foreach (var fn in func.GetInvocationList()) await ((Func<T1, T2, Task>)fn)(arg1, arg2);
    }
}