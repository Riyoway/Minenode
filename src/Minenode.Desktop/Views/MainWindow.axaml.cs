using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Material.Icons;
using Material.Icons.Avalonia;
using Minenode.Desktop.Models;
using Minenode.Desktop.Services;

namespace Minenode.Desktop.Views;

public partial class MainWindow : Window
{
    private readonly RuntimeBridge _runtime = new();
    private IStorageFile? _currentFile;
    private bool _dirty;

    public MainWindow()
    {
        InitializeComponent();
        BuildPalette();

        EditorCanvas.SelectedNodeChanged += ShowInspector;
        EditorCanvas.WorkflowChanged += () =>
        {
            _dirty = true;
            UpdateDocumentTitle();
        };
        EditorCanvas.StatusMessage += message => SetStatus(message);
        _runtime.OutputReceived += message => Dispatcher.UIThread.Post(() => AppendLog(message));

        UpdateDocumentTitle();
        AppendLog("Minenode editor initialized.");
    }

    protected override void OnClosed(EventArgs e)
    {
        _runtime.Dispose();
        base.OnClosed(e);
    }

    private void BuildPalette(string? filter = null)
    {
        PalettePanel.Children.Clear();
        var query = filter?.Trim() ?? string.Empty;

        var matches = NodeRegistry.All
            .Where(definition => string.IsNullOrWhiteSpace(query)
                || definition.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                || definition.Category.Contains(query, StringComparison.OrdinalIgnoreCase)
                || definition.Type.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 0)
        {
            var empty = new TextBlock
            {
                Text = "No nodes match this search.",
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(4, 10)
            };
            empty.Classes.Add("muted");
            PalettePanel.Children.Add(empty);
            return;
        }

        foreach (var group in matches.GroupBy(x => x.Category))
        {
            var category = new TextBlock
            {
                Text = group.Key.ToUpperInvariant(),
                Margin = new Thickness(4, 12, 0, 4)
            };
            category.Classes.Add("sectionLabel");
            PalettePanel.Children.Add(category);

            foreach (var definition in group)
            {
                var content = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("28,*,Auto"),
                    ColumnSpacing = 8
                };

                var iconHost = new Border
                {
                    Width = 28,
                    Height = 28,
                    CornerRadius = new CornerRadius(7),
                    Background = new SolidColorBrush(Color.Parse("#181D24")),
                    Child = CreateNodeIcon(definition.Type)
                };
                Grid.SetColumn(iconHost, 0);
                content.Children.Add(iconHost);

                var labels = new StackPanel
                {
                    Spacing = 0,
                    VerticalAlignment = VerticalAlignment.Center,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = definition.Title,
                            FontSize = 11,
                            FontWeight = FontWeight.SemiBold
                        },
                        new TextBlock
                        {
                            Text = definition.Type,
                            FontSize = 9,
                            Foreground = new SolidColorBrush(Color.Parse("#6F7885"))
                        }
                    }
                };
                Grid.SetColumn(labels, 1);
                content.Children.Add(labels);

                var addIcon = CreateIcon("Plus", 14);
                addIcon.Foreground = new SolidColorBrush(Color.Parse("#6F7885"));
                Grid.SetColumn(addIcon, 2);
                content.Children.Add(addIcon);

                var button = new Button
                {
                    Content = content,
                    Tag = definition.Type
                };
                button.Classes.Add("nodePaletteButton");
                button.Click += (_, _) => EditorCanvas.AddNode((string)button.Tag!);
                PalettePanel.Children.Add(button);
            }
        }
    }

    private static Control CreateNodeIcon(string nodeType)
    {
        var iconName = nodeType switch
        {
            "account.microsoft" => "AccountOutline",
            "connection.join" => "ServerNetworkOutline",
            "trigger.onSpawn" => "LoginVariant",
            "trigger.onChat" => "MessageTextOutline",
            "logic.if" => "SourceBranch",
            "utility.wait" => "TimerSand",
            "utility.setVariable" => "Variable",
            "utility.log" => "TextBoxOutline",
            "action.sendChat" => "MessageArrowRightOutline",
            "action.sendCommand" => "Console",
            "action.disconnect" => "Connection",
            _ => "CircleOutline"
        };

        var icon = CreateIcon(iconName, 16);
        icon.Foreground = new SolidColorBrush(Color.Parse("#AAB3BF"));
        return icon;
    }

    private static MaterialIcon CreateIcon(string name, double size)
    {
        var icon = new MaterialIcon
        {
            Width = size,
            Height = size,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        if (Enum.TryParse<MaterialIconKind>(name, ignoreCase: true, out var kind))
            icon.Kind = kind;

        return icon;
    }

    private void ShowInspector(NodeModel? node)
    {
        InspectorPanel.Children.Clear();

        if (node is null)
        {
            InspectorPanel.Children.Add(new TextBlock
            {
                Text = "Nothing selected",
                FontSize = 13,
                FontWeight = FontWeight.SemiBold
            });

            var hint = new TextBlock
            {
                Text = "Select a node on the canvas to inspect and edit its configuration.",
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap
            };
            hint.Classes.Add("muted");
            InspectorPanel.Children.Add(hint);
            return;
        }

        var definition = NodeRegistry.Get(node.Type);

        var heading = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("36,*"),
            ColumnSpacing = 10,
            Margin = new Thickness(0, 0, 0, 8)
        };

        heading.Children.Add(new Border
        {
            Width = 36,
            Height = 36,
            CornerRadius = new CornerRadius(9),
            Background = new SolidColorBrush(Color.Parse("#181D24")),
            Child = CreateNodeIcon(node.Type)
        });

        var titleStack = new StackPanel
        {
            Spacing = 1,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock
                {
                    Text = definition.Title,
                    FontSize = 13,
                    FontWeight = FontWeight.SemiBold
                },
                new TextBlock
                {
                    Text = definition.Category,
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.Parse("#8D96A3"))
                }
            }
        };
        Grid.SetColumn(titleStack, 1);
        heading.Children.Add(titleStack);
        InspectorPanel.Children.Add(heading);

        var typeLabel = new TextBlock
        {
            Text = node.Type,
            FontSize = 10,
            FontFamily = new FontFamily("Cascadia Mono, Consolas, monospace"),
            Foreground = new SolidColorBrush(Color.Parse("#707987")),
            Margin = new Thickness(0, 0, 0, 8)
        };
        InspectorPanel.Children.Add(typeLabel);

        if (definition.Fields.Count == 0)
        {
            var noFields = new TextBlock
            {
                Text = "This node has no configurable fields.",
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap
            };
            noFields.Classes.Add("muted");
            InspectorPanel.Children.Add(noFields);
            return;
        }

        foreach (var field in definition.Fields)
        {
            var label = new TextBlock
            {
                Text = field.Label,
                FontSize = 10,
                FontWeight = FontWeight.SemiBold,
                Margin = new Thickness(1, 7, 0, 0)
            };
            InspectorPanel.Children.Add(label);

            var textBox = new TextBox
            {
                Text = node.Config.GetValueOrDefault(field.Key, field.DefaultValue),
                Watermark = field.Hint
            };
            textBox.TextChanged += (_, _) =>
            {
                node.Config[field.Key] = textBox.Text ?? string.Empty;
                _dirty = true;
                UpdateDocumentTitle();
            };
            InspectorPanel.Children.Add(textBox);

            if (!string.IsNullOrWhiteSpace(field.Hint))
            {
                var hint = new TextBlock
                {
                    Text = field.Hint,
                    FontSize = 9,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(1, -2, 0, 0)
                };
                hint.Classes.Add("muted");
                InspectorPanel.Children.Add(hint);
            }
        }
    }

    private void NodeSearch_TextChanged(object? sender, TextChangedEventArgs e) =>
        BuildPalette(NodeSearchBox.Text);

    private void Minimize_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void Maximize_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Close_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();

    private void ClearLog_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        LogBox.Text = string.Empty;
        SetStatus("Log cleared");
    }

    private void New_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _runtime.Stop();
        _currentFile = null;
        _dirty = false;
        EditorCanvas.SetWorkflow(Controls.NodeCanvas.CreateNewWorkflow());
        LogBox.Text = string.Empty;
        AppendLog("Created a new workflow.");
        UpdateDocumentTitle();
    }

    private async void Import_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import Minenode workflow",
            AllowMultiple = false,
            FileTypeFilter = [WorkflowFileType()]
        });

        var file = files.FirstOrDefault();
        if (file is null)
            return;

        try
        {
            await using var stream = await file.OpenReadAsync();
            using var reader = new StreamReader(stream);
            var document = WorkflowSerializer.Deserialize(await reader.ReadToEndAsync());
            EditorCanvas.SetWorkflow(document);
            _currentFile = file;
            _dirty = false;
            AppendLog($"Imported {file.Name}.");
            UpdateDocumentTitle();
        }
        catch (Exception ex)
        {
            AppendLog($"Import failed: {ex.Message}");
            SetStatus("Import failed");
        }
    }

    private async void Save_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_currentFile is null)
        {
            _currentFile = await PickSaveFileAsync("Save Minenode workflow");
            if (_currentFile is null)
                return;
        }

        if (await SaveToFileAsync(_currentFile))
        {
            _dirty = false;
            UpdateDocumentTitle();
            SetStatus("Saved");
        }
    }

    private async void Export_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var file = await PickSaveFileAsync("Export Minenode workflow");
        if (file is null)
            return;

        if (await SaveToFileAsync(file))
            SetStatus($"Exported {file.Name}");
    }

    private void Delete_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        EditorCanvas.DeleteSelected();

    private async void Run_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var errors = WorkflowValidator.Validate(EditorCanvas.Workflow);
        if (errors.Count > 0)
        {
            AppendLog("Workflow validation failed:");
            foreach (var error in errors)
                AppendLog($"  - {error}");
            SetStatus("Validation failed");
            return;
        }

        try
        {
            AppendLog("Starting workflow runtime...");
            await _runtime.StartAsync(EditorCanvas.Workflow);
            SetStatus("Running");
        }
        catch (Exception ex)
        {
            AppendLog($"Runtime failed: {ex.Message}");
            SetStatus("Runtime error");
        }
    }

    private void Stop_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _runtime.Stop();
        AppendLog("Workflow stopped.");
        SetStatus("Stopped");
    }

    private async Task<IStorageFile?> PickSaveFileAsync(string title) =>
        await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = _currentFile?.Name ?? "workflow.minenode",
            DefaultExtension = "minenode",
            FileTypeChoices = [WorkflowFileType()]
        });

    private async Task<bool> SaveToFileAsync(IStorageFile file)
    {
        try
        {
            await using var stream = await file.OpenWriteAsync();
            stream.SetLength(0);
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(WorkflowSerializer.Serialize(EditorCanvas.Workflow));
            await writer.FlushAsync();
            AppendLog($"Saved {file.Name}.");
            return true;
        }
        catch (Exception ex)
        {
            AppendLog($"Save failed: {ex.Message}");
            SetStatus("Save failed");
            return false;
        }
    }

    private static FilePickerFileType WorkflowFileType() => new("Minenode workflow")
    {
        Patterns = ["*.minenode", "*.json"],
        MimeTypes = ["application/json"]
    };

    private void AppendLog(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        LogBox.Text = string.IsNullOrEmpty(LogBox.Text)
            ? $"[{timestamp}] {message}"
            : $"{LogBox.Text}{Environment.NewLine}[{timestamp}] {message}";
        LogBox.CaretIndex = LogBox.Text?.Length ?? 0;
    }

    private void SetStatus(string message) => StatusText.Text = message;

    private void UpdateDocumentTitle()
    {
        var name = _currentFile?.Name ?? EditorCanvas.Workflow.Name;
        DocumentNameText.Text = _dirty ? $"{name} *" : name;
        Title = _dirty ? $"Minenode — {name} *" : $"Minenode — {name}";
    }
}
