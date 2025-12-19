using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ControlPanel.Agent.Shared;
using ControlPanel.Shared;
using ControlPanel.Shared.Extensions;

// ReSharper disable ClassNeverInstantiated.Local

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

public class PipeWireAudioAgent : IAudioAgent
{
    public Task<AudioAgentDescription> GetAudioAgentDescription()
    {
        return Task.FromResult(new AudioAgentDescription(
            AgentIcon: ResourceLoader.Load("Assets/linux-logo.svg").ReadAllBytes()
        ));
    }

    public async Task<AudioStream[]> GetAudioStreamsAsync(CancellationToken cancellationToken)
    {
        var streams = (await GetPipeWireNodes(cancellationToken))
            .Where(x => x.Info is { Params.Props.Length: > 0, Props.Count: > 0 })
            .Where(x => x.Type == "PipeWire:Interface:Node" 
                        && x.Info!.Props.TryGetValue("media.class", out var v) && v.GetValue<string>() == "Stream/Output/Audio"
                        && x.Info.State == "running")
            .Select(x =>
            {
                // to mute nullable warnings
                var info = x.Info!;
                var props = x.Info!.Params!.Props![0];
                return new AudioStream(
                    Id: x.Id.ToString(),
                    Source: GetBinaryName(info.Props),
                    Name: BuildDisplayName(x.Id, info.Props),
                    Mute: props.Mute,
                    Volume: Math.Pow(props.ChannelVolumes.Average(), 1.0 / 3));
            })
            .ToArray();

        return streams;
    }

    public async Task SetVolumeAsync(string id, double volume, CancellationToken cancellationToken)
    {
        volume = Math.Pow(volume, 3); // from cubic to linear
        await ProcessExecAsync("pw-cli", ["s", id, "Props", $"{{channelVolumes: [{volume:F2}, {volume:F2}]}}"],  cancellationToken);
    }

    public async Task ToggleMuteAsync(string id, bool mute, CancellationToken cancellationToken)
    {
        await ProcessExecAsync("pw-cli", ["s", id, "Props", $"{{mute: {(mute ? "true" : "false")}}}"],  cancellationToken);
    }

    public async Task<AudioStreamIcon> GetAudioStreamIconAsync(string source, CancellationToken cancellationToken)
    {
        var icon = IconLocator.FindIcon(source);
        
        return string.IsNullOrEmpty(icon)
            ? AudioStreamIcon.Default
            : new AudioStreamIcon(await File.ReadAllBytesAsync(icon, cancellationToken));
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
    
    private static bool IsGenericName(string? mediaName, string? appName, string? nodeDesc, string? nodeName)
    {
        if (string.IsNullOrWhiteSpace(mediaName))
            return true;

        // identical to app/description → not adding info
        if (!string.IsNullOrWhiteSpace(appName) && mediaName == appName)
            return true;
        if (!string.IsNullOrWhiteSpace(nodeDesc) && mediaName == nodeDesc)
            return true;
        
        // a few PipeWire-ish boring names — purely agent-local
        return mediaName is "Audio Stream" or "audio stream" or "Playback Stream"
               || mediaName.StartsWith("audio") && (nodeName?.StartsWith("qemu") ?? false);
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

        // 1) media.name when it seems specific
        if (!IsGenericName(mediaName, appName, nodeDesc, nodeName))
        {
            return !string.IsNullOrWhiteSpace(appName)
                ? $"{appName}: {mediaName}"
                : mediaName!;
        }

        // 2) node.description is usually nice for devices
        if (!string.IsNullOrWhiteSpace(nodeDesc))
            return nodeDesc;

        // 3) fall back to application.name
        if (!string.IsNullOrWhiteSpace(appName))
            return appName;

        // 4) last resort: node.name or id
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