# Minenode

Minenode is a native visual automation environment for Minecraft bots powered by Mineflayer. Workflows are assembled by connecting typed nodes into small programs, combining account contexts, server connections, events, conditions, delays, and actions without writing JavaScript.

The editor is designed around a Scratch-like graph model rather than a generic automation canvas: Minecraft accounts and bot contexts are first-class workflow values, node ports are typed, and execution state is tied to a specific Mineflayer bot instance.

## Current scope

The initial implementation includes:

- Native desktop node editor built with Avalonia UI; no Electron or browser-based editor.
- Microsoft account nodes with support for multiple account contexts.
- Join Server, On Spawn, and On Chat nodes.
- If, Wait, Set Variable, Log, Send Chat, Send Command, and Disconnect nodes.
- Typed connections between `Bot` and `Flow` ports.
- Drag-and-drop node positioning and visual connections.
- Inspector-based node configuration.
- Workflow validation before execution.
- Native save/open support for `.minenode` workflow files.
- JSON import/export using the same versioned workflow format.
- A separate Node.js Mineflayer runtime process so bot failures are isolated from the editor UI.
- Runtime log streaming back into the editor.

## Architecture

```text
Minenode.Desktop (.NET 10 / Avalonia)
├─ Native node canvas
├─ Workflow model + validation
├─ Save / import / export
└─ Runtime bridge
         │ JSON lines over stdin/stdout
         ▼
Minenode Runtime (Node.js)
└─ Mineflayer
   ├─ Microsoft authentication
   ├─ Server connections
   └─ Workflow execution
```

The UI does not embed a web frontend or Electron runtime. Mineflayer remains in a separate Node.js process because it is part of the PrismarineJS ecosystem and can be restarted independently from the editor.

## Workflow model

A workflow is a versioned JSON document. Nodes store their type, canvas position, and configuration. Connections store source and target node/port identifiers.

```json
{
  "format": "minenode-workflow",
  "version": 1,
  "name": "Ping Bot",
  "nodes": [],
  "connections": []
}
```

Typical flow:

```text
Microsoft Account ─Bot→ Join Server ─Bot→ On Chat ─Flow→ If ─true→ Send Chat
```

Event data can be referenced in node fields with templates such as `{{message}}`, `{{sender}}`, `{{username}}`, `{{x}}`, `{{y}}`, and `{{z}}`.

## Requirements

- .NET 10 SDK
- Node.js 18 or newer
- A Minecraft Java Edition account for Microsoft-authenticated servers

## Development

Install the Mineflayer runtime dependencies:

```bash
cd runtime
npm install
```

Run the native editor from the repository root:

```bash
dotnet run --project src/Minenode.Desktop/Minenode.Desktop.csproj
```

The runtime is started automatically by the editor when a workflow is executed.

## Repository layout

```text
runtime/                    Mineflayer execution process
src/Minenode.Desktop/       Native Avalonia editor
```

## Workflow files

`.minenode` files are portable UTF-8 JSON files. The editor supports opening an existing workflow, saving changes, exporting a copy, and importing compatible version-1 documents.

## Status

Minenode is under active development. The current milestone focuses on the visual programming core, persistence, basic event/action execution, Microsoft authentication, and server connectivity. Inventory, pathfinding, block/entity interaction, multi-bot grouping, and plugin nodes are planned as later runtime extensions.
