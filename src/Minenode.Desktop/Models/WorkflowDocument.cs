using System.Text.Json.Serialization;

namespace Minenode.Desktop.Models;

public sealed class WorkflowDocument
{
    [JsonPropertyName("format")]
    public string Format { get; set; } = "minenode-workflow";

    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("name")]
    public string Name { get; set; } = "Untitled";

    [JsonPropertyName("nodes")]
    public List<NodeModel> Nodes { get; set; } = [];

    [JsonPropertyName("connections")]
    public List<ConnectionModel> Connections { get; set; } = [];
}

public sealed class NodeModel
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("x")]
    public double X { get; set; }

    [JsonPropertyName("y")]
    public double Y { get; set; }

    [JsonPropertyName("config")]
    public Dictionary<string, string> Config { get; set; } = new(StringComparer.Ordinal);
}

public sealed class ConnectionModel
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("sourceNode")]
    public string SourceNode { get; set; } = string.Empty;

    [JsonPropertyName("sourcePort")]
    public string SourcePort { get; set; } = string.Empty;

    [JsonPropertyName("targetNode")]
    public string TargetNode { get; set; } = string.Empty;

    [JsonPropertyName("targetPort")]
    public string TargetPort { get; set; } = string.Empty;
}
