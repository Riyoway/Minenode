using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Minenode.Desktop.Models;

namespace Minenode.Desktop.Controls;

public sealed class NodeCanvas : UserControl
{
    private const double NodeWidth = 210;
    private const double HeaderHeight = 52;
    private const double PortRowHeight = 24;

    private readonly Grid _root = new();
    private readonly Canvas _surface = new();
    private readonly ConnectionLayer _connections;
    private readonly Dictionary<string, Border> _views = new(StringComparer.Ordinal);

    private (string NodeId, string PortId)? _pendingSource;
    private NodeModel? _selectedNode;
    private WorkflowDocument _workflow = CreateNewWorkflow();

    public event Action<NodeModel?>? SelectedNodeChanged;
    public event Action? WorkflowChanged;
    public event Action<string>? StatusMessage;

    public WorkflowDocument Workflow => _workflow;

    public NodeCanvas()
    {
        _connections = new ConnectionLayer(this) { IsHitTestVisible = false };
        _root.Children.Add(_connections);
        _root.Children.Add(_surface);
        Content = _root;

        _surface.Background = new SolidColorBrush(Color.Parse("#111111"));
        _surface.PointerPressed += (_, e) =>
        {
            if (e.Source == _surface)
                SelectNode(null);
        };

        Rebuild();
    }

    public static WorkflowDocument CreateNewWorkflow()
    {
        var document = new WorkflowDocument();
        document.Nodes.Add(NodeRegistry.Create("account.microsoft", 80, 80));
        return document;
    }

    public void SetWorkflow(WorkflowDocument workflow)
    {
        _workflow = workflow;
        _pendingSource = null;
        _selectedNode = null;
        Rebuild();
        SelectedNodeChanged?.Invoke(null);
    }

    public void AddNode(string type)
    {
        if (type != "account.microsoft")
        {
            var configuredAccount = _workflow.Nodes.Any(x =>
                x.Type == "account.microsoft" &&
                !string.IsNullOrWhiteSpace(x.Config.GetValueOrDefault("username")));

            if (!configuredAccount)
            {
                StatusMessage?.Invoke("Configure a Microsoft Account node before adding other nodes.");
                return;
            }
        }

        var offset = 40 * (_workflow.Nodes.Count % 8);
        var node = NodeRegistry.Create(type, 280 + offset, 100 + offset);
        _workflow.Nodes.Add(node);
        AddNodeView(node);
        SelectNode(node);
        MarkChanged();
    }

    public void DeleteSelected()
    {
        if (_selectedNode is null)
            return;

        if (_selectedNode.Type == "account.microsoft" &&
            _workflow.Nodes.Count(x => x.Type == "account.microsoft") <= 1)
        {
            StatusMessage?.Invoke("The final Microsoft Account node cannot be deleted.");
            return;
        }

        var id = _selectedNode.Id;
        _workflow.Nodes.RemoveAll(x => x.Id == id);
        _workflow.Connections.RemoveAll(x => x.SourceNode == id || x.TargetNode == id);
        _selectedNode = null;
        Rebuild();
        SelectedNodeChanged?.Invoke(null);
        MarkChanged();
    }

    public void RefreshSelectedNode() =>
        SelectedNodeChanged?.Invoke(_selectedNode);

    private void Rebuild()
    {
        _surface.Children.Clear();
        _views.Clear();
        foreach (var node in _workflow.Nodes)
            AddNodeView(node);
        _connections.InvalidateVisual();
    }

    private void AddNodeView(NodeModel node)
    {
        if (!NodeRegistry.TryGet(node.Type, out var definition) || definition is null)
            return;

        foreach (var field in definition.Fields)
            node.Config.TryAdd(field.Key, field.DefaultValue);

        var root = new Border
        {
            Width = NodeWidth,
            Background = new SolidColorBrush(Color.Parse("#1A1A1A")),
            BorderBrush = new SolidColorBrush(Color.Parse("#3A3A3A")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(0),
            ClipToBounds = true
        };

        var panel = new StackPanel { Spacing = 0 };
        var header = new Border
        {
            Height = HeaderHeight,
            Background = new SolidColorBrush(Color.Parse("#232323")),
            Padding = new Thickness(12, 7),
            Child = new StackPanel
            {
                Spacing = 1,
                Children =
                {
                    new TextBlock
                    {
                        Text = definition.Title,
                        FontWeight = FontWeight.SemiBold,
                        FontSize = 14
                    },
                    new TextBlock
                    {
                        Text = definition.Category,
                        Opacity = 0.58,
                        FontSize = 11
                    }
                }
            }
        };

        panel.Children.Add(header);

        var maxPorts = Math.Max(definition.Inputs.Count, definition.Outputs.Count);
        for (var i = 0; i < maxPorts; i++)
        {
            var row = new Grid
            {
                Height = PortRowHeight,
                ColumnDefinitions = new ColumnDefinitions("*,*")
            };

            if (i < definition.Inputs.Count)
            {
                var input = definition.Inputs[i];
                var inputPanel = CreatePort(node, input, isOutput: false);
                Grid.SetColumn(inputPanel, 0);
                row.Children.Add(inputPanel);
            }

            if (i < definition.Outputs.Count)
            {
                var output = definition.Outputs[i];
                var outputPanel = CreatePort(node, output, isOutput: true);
                Grid.SetColumn(outputPanel, 1);
                row.Children.Add(outputPanel);
            }

            panel.Children.Add(row);
        }

        root.Child = panel;
        root.PointerPressed += (_, e) =>
        {
            SelectNode(node);
            e.Handled = true;
        };

        var dragging = false;
        Point pointerOffset = default;

        header.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(header).Properties.IsLeftButtonPressed)
                return;

            SelectNode(node);
            dragging = true;
            var point = e.GetPosition(_surface);
            pointerOffset = new Point(point.X - node.X, point.Y - node.Y);
            e.Pointer.Capture(header);
            e.Handled = true;
        };

        header.PointerMoved += (_, e) =>
        {
            if (!dragging)
                return;

            var point = e.GetPosition(_surface);
            node.X = Math.Max(0, point.X - pointerOffset.X);
            node.Y = Math.Max(0, point.Y - pointerOffset.Y);
            Canvas.SetLeft(root, node.X);
            Canvas.SetTop(root, node.Y);
            _connections.InvalidateVisual();
            e.Handled = true;
        };

        header.PointerReleased += (_, e) =>
        {
            if (!dragging)
                return;

            dragging = false;
            e.Pointer.Capture(null);
            MarkChanged();
            e.Handled = true;
        };

        Canvas.SetLeft(root, node.X);
        Canvas.SetTop(root, node.Y);
        _surface.Children.Add(root);
        _views[node.Id] = root;
    }

    private Control CreatePort(NodeModel node, PortDefinition port, bool isOutput)
    {
        var dot = new Border
        {
            Width = 12,
            Height = 12,
            CornerRadius = new CornerRadius(6),
            Background = new SolidColorBrush(Color.Parse(port.Type == "bot" ? "#B8B8B8" : "#737373")),
            BorderBrush = new SolidColorBrush(Color.Parse("#E3E3E3")),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(6, 0)
        };

        dot.PointerPressed += (_, e) =>
        {
            HandlePort(node, port, isOutput);
            e.Handled = true;
        };

        var label = new TextBlock
        {
            Text = port.Label,
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.75
        };

        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = isOutput ? HorizontalAlignment.Right : HorizontalAlignment.Left
        };

        if (isOutput)
        {
            panel.Children.Add(label);
            panel.Children.Add(dot);
        }
        else
        {
            panel.Children.Add(dot);
            panel.Children.Add(label);
        }

        return panel;
    }

    private void HandlePort(NodeModel node, PortDefinition port, bool isOutput)
    {
        if (isOutput)
        {
            _pendingSource = (node.Id, port.Id);
            StatusMessage?.Invoke($"Connect {NodeRegistry.Get(node.Type).Title}.{port.Label} to a compatible input.");
            return;
        }

        if (_pendingSource is null)
        {
            StatusMessage?.Invoke("Select an output port first.");
            return;
        }

        var sourceNode = _workflow.Nodes.FirstOrDefault(x => x.Id == _pendingSource.Value.NodeId);
        if (sourceNode is null || sourceNode.Id == node.Id)
        {
            _pendingSource = null;
            StatusMessage?.Invoke("A node cannot connect to itself.");
            return;
        }

        var sourceDefinition = NodeRegistry.Get(sourceNode.Type);
        var sourcePort = sourceDefinition.Outputs.FirstOrDefault(x => x.Id == _pendingSource.Value.PortId);
        if (sourcePort is null || !string.Equals(sourcePort.Type, port.Type, StringComparison.Ordinal))
        {
            _pendingSource = null;
            StatusMessage?.Invoke("Port types do not match.");
            return;
        }

        _workflow.Connections.RemoveAll(x => x.TargetNode == node.Id && x.TargetPort == port.Id);
        _workflow.Connections.Add(new ConnectionModel
        {
            SourceNode = sourceNode.Id,
            SourcePort = sourcePort.Id,
            TargetNode = node.Id,
            TargetPort = port.Id
        });

        _pendingSource = null;
        _connections.InvalidateVisual();
        MarkChanged();
        StatusMessage?.Invoke("Connected.");
    }

    private void SelectNode(NodeModel? node)
    {
        _selectedNode = node;

        foreach (var pair in _views)
        {
            pair.Value.BorderBrush = new SolidColorBrush(Color.Parse(
                node is not null && pair.Key == node.Id ? "#E6E6E6" : "#3A3A3A"));
            pair.Value.BorderThickness = new Thickness(
                node is not null && pair.Key == node.Id ? 2 : 1);
        }

        SelectedNodeChanged?.Invoke(node);
    }

    private void MarkChanged()
    {
        _connections.InvalidateVisual();
        WorkflowChanged?.Invoke();
    }

    private Point GetPortPoint(string nodeId, string portId, bool output)
    {
        var node = _workflow.Nodes.First(x => x.Id == nodeId);
        var definition = NodeRegistry.Get(node.Type);
        var ports = output ? definition.Outputs : definition.Inputs;
        var index = ports.ToList().FindIndex(x => x.Id == portId);
        if (index < 0)
            index = 0;

        var x = output ? node.X + NodeWidth : node.X;
        var y = node.Y + HeaderHeight + (PortRowHeight * index) + (PortRowHeight / 2);
        return new Point(x, y);
    }

    private sealed class ConnectionLayer(NodeCanvas owner) : Control
    {
        public override void Render(DrawingContext context)
        {
            base.Render(context);

            var pen = new Pen(new SolidColorBrush(Color.Parse("#777777")), 2);
            foreach (var connection in owner._workflow.Connections)
            {
                if (!owner._workflow.Nodes.Any(x => x.Id == connection.SourceNode) ||
                    !owner._workflow.Nodes.Any(x => x.Id == connection.TargetNode))
                    continue;

                var start = owner.GetPortPoint(connection.SourceNode, connection.SourcePort, output: true);
                var end = owner.GetPortPoint(connection.TargetNode, connection.TargetPort, output: false);
                var middleX = start.X + ((end.X - start.X) / 2);

                context.DrawLine(pen, start, new Point(middleX, start.Y));
                context.DrawLine(pen, new Point(middleX, start.Y), new Point(middleX, end.Y));
                context.DrawLine(pen, new Point(middleX, end.Y), end);
            }
        }
    }
}
