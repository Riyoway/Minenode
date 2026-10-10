using Minenode.Desktop.Models;

namespace Minenode.Desktop.Services;

public static class WorkflowValidator
{
    public static IReadOnlyList<string> Validate(WorkflowDocument workflow)
    {
        var errors = new List<string>();
        var nodes = workflow.Nodes.ToDictionary(x => x.Id, StringComparer.Ordinal);

        if (!workflow.Nodes.Any(x => x.Type == "account.microsoft"))
            errors.Add("At least one Microsoft Account node is required.");

        if (!workflow.Nodes.Any(x => x.Type == "connection.join"))
            errors.Add("At least one Join Server node is required.");

        foreach (var account in workflow.Nodes.Where(x => x.Type == "account.microsoft"))
        {
            if (string.IsNullOrWhiteSpace(account.Config.GetValueOrDefault("username")))
                errors.Add("Microsoft Account: account identifier is required.");
        }

        foreach (var node in workflow.Nodes)
        {
            if (!NodeRegistry.TryGet(node.Type, out var definition) || definition is null)
            {
                errors.Add($"Unknown node type: {node.Type}.");
                continue;
            }

            foreach (var input in definition.Inputs)
            {
                if (!workflow.Connections.Any(x => x.TargetNode == node.Id && x.TargetPort == input.Id))
                    errors.Add($"{definition.Title}: input '{input.Label}' is not connected.");
            }
        }

        foreach (var connection in workflow.Connections)
        {
            if (!nodes.TryGetValue(connection.SourceNode, out var source) ||
                !nodes.TryGetValue(connection.TargetNode, out var target))
            {
                errors.Add($"Connection {connection.Id} references a missing node.");
                continue;
            }

            if (!NodeRegistry.TryGet(source.Type, out var sourceDef) || sourceDef is null ||
                !NodeRegistry.TryGet(target.Type, out var targetDef) || targetDef is null)
                continue;

            var output = sourceDef.Outputs.FirstOrDefault(x => x.Id == connection.SourcePort);
            var input = targetDef.Inputs.FirstOrDefault(x => x.Id == connection.TargetPort);

            if (output is null || input is null)
            {
                errors.Add($"Connection {connection.Id} references an unknown port.");
                continue;
            }

            if (!string.Equals(output.Type, input.Type, StringComparison.Ordinal))
                errors.Add($"Type mismatch: {sourceDef.Title}.{output.Label} cannot connect to {targetDef.Title}.{input.Label}.");
        }

        return errors;
    }
}
