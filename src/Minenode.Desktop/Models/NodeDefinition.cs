namespace Minenode.Desktop.Models;

public sealed record PortDefinition(string Id, string Label, string Type);
public sealed record ConfigFieldDefinition(string Key, string Label, string DefaultValue, string? Hint = null);

public sealed record NodeDefinition(
    string Type,
    string Title,
    string Category,
    IReadOnlyList<PortDefinition> Inputs,
    IReadOnlyList<PortDefinition> Outputs,
    IReadOnlyList<ConfigFieldDefinition> Fields);
