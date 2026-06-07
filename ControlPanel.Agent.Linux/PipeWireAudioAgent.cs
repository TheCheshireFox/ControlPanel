using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ControlPanel.Agent.Shared;
using ControlPanel.Shared;
using ControlPanel.Shared.Extensions;

namespace ControlPanel.Agent.Linux;

internal record PipeWireNodeProps(
    [property: JsonPropertyName("mute")] bool Mute,
    [property: JsonPropertyName("channelVolumes")] double[] ChannelVolumes);

internal record PipeWireNodeParams(
    [property: JsonPropertyName("Props")] PipeWireNodeProps[]? Props);

internal record PipeWireNodeInfo(
    [property: JsonPropertyName("props")] Dictionary<string, JsonValue> Props,
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("params")] PipeWireNodeParams? Params);

internal record PipeWireNode(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("info")] PipeWireNodeInfo? Info);

internal static class DictionaryExtension
{
    public static T? GetProperty<T>(this IDictionary<string, JsonValue> props, string key, T? defaultValue = default)
        => props.TryGetValue(key, out var jsonValue) && jsonValue.TryGetValue<T>(out var value) ? value :  defaultValue;
}

internal class PipeWireAudioAgent(IIconLocator iconLocator) : IAudioAgent, IDisposable
{
    private readonly AudioStreamIconCache _iconCache = new(TimeSpan.FromHours(1), 4 * 1024 * 1024);
    
    public Task<AudioAgentDescription> GetAudioAgentDescription()
    {
        return Task.FromResult(new AudioAgentDescription(
            AgentIcon: ResourceLoader.Load("Assets/linux-logo.svg", GetType().Assembly).ReadAllBytes()
        ));
    }

    public async Task<AudioStream[]> GetAudioStreamsAsync(CancellationToken cancellationToken)
    {
        var nodes = (await GetPipeWireNodes(cancellationToken))
            .Where(x => x.Info is { Params.Props.Length: > 0, Props.Count: > 0 })
            .Where(x => x.Type == "PipeWire:Interface:Node" 
                        && x.Info!.Props.TryGetValue("media.class", out var v) && v.GetValue<string>() == "Stream/Output/Audio"
                        && x.Info.State == "running")
            .ToArray();

        var streams = new List<AudioStream>(nodes.Length);
        foreach (var node in nodes)
        {
            // to mute nullable warnings
            var info = node.Info!;
            var props = node.Info!.Params!.Props![0];
            var source = GetBinaryName(info.Props);
            var icon = await GetIconAsync(source, cancellationToken);

            streams.Add(new AudioStream(
                Id: node.Id.ToString(),
                Source: source,
                Name: BuildDisplayName(node.Id, info.Props),
                Mute: props.Mute,
                Volume: Math.Pow(props.ChannelVolumes.Average(), 1.0 / 3),
                IconHash: icon.IconHash));
        }

        return streams.ToArray();
    }

    public async Task SetVolumeAsync(string id, double volume, CancellationToken cancellationToken)
    {
        volume = Math.Pow(volume, 3); // from cubic to linear
        await ProcessExecAsync("pw-cli", ["s", id, "Props", $"{{channelVolumes: [{volume:F2}, {volume:F2}]}}"], cancellationToken);
    }

    public async Task ToggleMuteAsync(string id, bool mute, CancellationToken cancellationToken)
    {
        await ProcessExecAsync("pw-cli", ["s", id, "Props", $"{{mute: {(mute ? "true" : "false")}}}"], cancellationToken);
    }

    public async Task<AudioStreamIcon> GetAudioStreamIconAsync(string source, CancellationToken cancellationToken)
        => await GetIconAsync(source, cancellationToken);

    private async Task<AudioStreamIcon> GetIconAsync(string source, CancellationToken cancellationToken)
        => await _iconCache.GetOrAddAsync(source, ct => LoadIconAsync(source, ct), cancellationToken);

    private async Task<AudioStreamIcon> LoadIconAsync(string source, CancellationToken cancellationToken)
    {
        var iconPath = iconLocator.FindIcon(source);
        
        return string.IsNullOrEmpty(iconPath)
            ? AudioStreamIcon.Default
            : AudioStreamIcon.FromBytes(await File.ReadAllBytesAsync(iconPath, cancellationToken));
    }

    public void Dispose()
    {
        _iconCache.Dispose();
    }

    private static async Task<PipeWireNode[]> GetPipeWireNodes(CancellationToken cancellationToken)
    {
        var process = Process.Start(new ProcessStartInfo("pw-dump")
        {
            RedirectStandardOutput = true
        }) ?? throw new Exception("Unable to start pw-dump");

        var nodes = await JsonSerializer.DeserializeAsync<PipeWireNode[]>(process.StandardOutput.BaseStream, cancellationToken: cancellationToken)
               ?? throw new Exception("Unable to deserialize json audio streams");
        
        await process.WaitForExitAsync(cancellationToken);
        
        return nodes;
    }
    
    private static bool IsGenericName(string? mediaName, string? appName, string? nodeDesc)
    {
        if (string.IsNullOrWhiteSpace(mediaName))
            return true;

        if (!string.IsNullOrWhiteSpace(appName) && mediaName == appName)
            return true;
        if (!string.IsNullOrWhiteSpace(nodeDesc) && mediaName == nodeDesc)
            return true;

        return mediaName is "Audio Stream" or "audio stream" or "Playback Stream";
    }

    private static string GetBinaryName(Dictionary<string, JsonValue> props)
    {
        var name = props.GetProperty<string>("application.process.binary");
        if (!string.IsNullOrEmpty(name))
            return name;
        
        var pid = props.GetProperty<int>("application.process.id");
        if (pid > 0)
            return ProcessUtility.GetBinaryPath(pid) ?? string.Empty;
        
        return string.Empty;
    }
    
    private static string BuildDisplayName(int id, Dictionary<string, JsonValue> props)
    {
        var mediaName = props.GetProperty<string>("media.name");
        var appName = props.GetProperty<string>("application.name");
        var nodeDesc = props.GetProperty<string>("node.description");
        var nodeName = props.GetProperty<string>("node.name");

        if (!IsGenericName(mediaName, appName, nodeDesc))
        {
            return !string.IsNullOrWhiteSpace(appName)
                ? $"{appName}: {mediaName}"
                : mediaName!;
        }

        if (!string.IsNullOrWhiteSpace(nodeDesc))
            return nodeDesc;

        if (!string.IsNullOrWhiteSpace(appName))
            return appName;

        if (!string.IsNullOrWhiteSpace(nodeName))
            return nodeName;

        return $"Stream {id}";
    }
    
    private static async Task ProcessExecAsync(string program, string[] args, CancellationToken cancellationToken)
    {
        var process = Process.Start(new ProcessStartInfo(program, args)
        {
            RedirectStandardError = true
        }) ?? throw new Exception($"Unable to start {program}");
        
        var readTask = Task.Run(async () => await process.StandardError.ReadToEndAsync(cancellationToken), cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        
        var error = await readTask;
        if (!string.IsNullOrEmpty(error) || process.ExitCode != 0)
            throw new Exception($"{program} failed with error: {error}");
    }
}