using System.Text.Json;
using Minenode.Desktop.Models;

namespace Minenode.Desktop.Services;

public static class WorkflowSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static string Serialize(WorkflowDocument document) => JsonSerializer.Serialize(document, Options);

    public static WorkflowDocument Deserialize(string json)
    {
        var workflow = JsonSerializer.Deserialize<WorkflowDocument>(json, Options)
            ?? throw new InvalidDataException("The workflow document is empty.");

        if (!string.Equals(workflow.Format, "minenode-workflow", StringComparison.Ordinal))
            throw new InvalidDataException("Unsupported workflow format.");

        if (workflow.Version != 1)
            throw new InvalidDataException($"Unsupported workflow version: {workflow.Version}.");

        workflow.Nodes ??= [];
        workflow.Connections ??= [];
        foreach (var node in workflow.Nodes)
            node.Config ??= new Dictionary<string, string>(StringComparer.Ordinal);

        return workflow;
    }
}
