using System.Reflection;

namespace ControlPanel.Shared;

public static class ResourceLoader
{
    public static Stream Load(string name, Assembly? assembly = null)
    {
        assembly ??= Assembly.GetCallingAssembly();
        var assemblyName = assembly.FullName!.Split(',')[0];

        string resourcePath;
        if (name.StartsWith(assemblyName))
        {
            resourcePath = name;
        }
        else
        {
            name = name.Replace('/', '.');
            if (name.StartsWith('.'))
                name = name[1..];
            
            resourcePath = $"{assemblyName}.{name}";
        }

        return assembly.GetManifestResourceStream(resourcePath) ?? throw new Exception($"Resource {resourcePath} not found");
    }
}