namespace Minenode.Desktop.Models;

public static class NodeRegistry
{
    public static readonly IReadOnlyList<NodeDefinition> All =
    [
        new(
            "account.microsoft",
            "Microsoft Account",
            "Account",
            [],
            [new("bot", "Bot", "bot")],
            [new("username", "Account identifier", "", "Stable identifier used for the Microsoft authentication cache")]),

        new(
            "connection.join",
            "Join Server",
            "Connection",
            [new("bot", "Bot", "bot")],
            [new("bot", "Bot", "bot")],
            [
                new("host", "Host", "localhost"),
                new("port", "Port", "25565"),
                new("version", "Version", "auto", "Use auto to let Mineflayer detect the server version")
            ]),

        new(
            "trigger.onSpawn",
            "On Spawn",
            "Trigger",
            [new("bot", "Bot", "bot")],
            [new("flow", "Flow", "flow")],
            []),

        new(
            "trigger.onChat",
            "On Chat",
            "Trigger",
            [new("bot", "Bot", "bot")],
            [new("flow", "Flow", "flow")],
            []),

        new(
            "logic.if",
            "If",
            "Logic",
            [new("flow", "Flow", "flow")],
            [new("true", "True", "flow"), new("false", "False", "flow")],
            [
                new("left", "Left", "{{message}}"),
                new("operator", "Operator", "contains", "contains, equals, notEquals, startsWith, endsWith, regex"),
                new("right", "Right", "!ping")
            ]),

        new(
            "utility.wait",
            "Wait",
            "Utility",
            [new("flow", "Flow", "flow")],
            [new("flow", "Flow", "flow")],
            [new("milliseconds", "Milliseconds", "1000")]),

        new(
            "utility.setVariable",
            "Set Variable",
            "Utility",
            [new("flow", "Flow", "flow")],
            [new("flow", "Flow", "flow")],
            [
                new("name", "Name", "value"),
                new("value", "Value", "{{message}}")
            ]),

        new(
            "utility.log",
            "Log",
            "Utility",
            [new("flow", "Flow", "flow")],
            [new("flow", "Flow", "flow")],
            [new("message", "Message", "{{message}}")]),

        new(
            "action.sendChat",
            "Send Chat",
            "Action",
            [new("flow", "Flow", "flow")],
            [new("flow", "Flow", "flow")],
            [new("message", "Message", "pong!")]),

        new(
            "action.sendCommand",
            "Send Command",
            "Action",
            [new("flow", "Flow", "flow")],
            [new("flow", "Flow", "flow")],
            [new("command", "Command", "/help")]),

        new(
            "action.disconnect",
            "Disconnect",
            "Action",
            [new("flow", "Flow", "flow")],
            [],
            [new("reason", "Reason", "Minenode workflow")])
    ];

    public static NodeDefinition Get(string type) =>
        All.FirstOrDefault(x => x.Type == type)
        ?? throw new InvalidOperationException($"Unknown node type: {type}");

    public static bool TryGet(string type, out NodeDefinition? definition)
    {
        definition = All.FirstOrDefault(x => x.Type == type);
        return definition is not null;
    }

    public static NodeModel Create(string type, double x, double y)
    {
        var definition = Get(type);
        return new NodeModel
        {
            Type = type,
            X = x,
            Y = y,
            Config = definition.Fields.ToDictionary(x => x.Key, x => x.DefaultValue, StringComparer.Ordinal)
        };
    }
}
