using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
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

    private void BuildPalette()
    {
        PalettePanel.Children.Clear();
        foreach (var group in NodeRegistry.All.GroupBy(x => x.Category))
        {
            PalettePanel.Children.Add(new TextBlock
            {
                Text = group.Key.ToUpperInvariant(),
                FontSize = 10,
                Opacity = 0.48,
                Margin = new Avalonia.Thickness(2, 10, 0, 2)
            });

            foreach (var definition in group)
            {
                var button = new Button
                {
                    Content = definition.Title,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Tag = definition.Type
                };
                button.Click += (_, _) => EditorCanvas.AddNode((string)button.Tag!);
                PalettePanel.Children.Add(button);
            }
        }
    }

    private void ShowInspector(NodeModel? node)
    {
        InspectorPanel.Children.Clear();
        InspectorPanel.Children.Add(new TextBlock
        {
            Text = "Inspector",
            FontSize = 16,
            FontWeight = Avalonia.Media.FontWeight.SemiBold
        });

        if (node is null)
        {
            InspectorPanel.Children.Add(new TextBlock
            {
                Text = "Select a node to edit its settings.",
                Opacity = 0.55,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            });
            return;
        }

        var definition = NodeRegistry.Get(node.Type);
        InspectorPanel.Children.Add(new TextBlock
        {
            Text = definition.Title,
            FontSize = 14,
            Margin = new Avalonia.Thickness(0, 4, 0, 8)
        });

        if (definition.Fields.Count == 0)
        {
            InspectorPanel.Children.Add(new TextBlock
            {
                Text = "This node has no configurable fields.",
                Opacity = 0.55,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            });
            return;
        }

        foreach (var field in definition.Fields)
        {
            InspectorPanel.Children.Add(new TextBlock
            {
                Text = field.Label,
                FontSize = 11,
                Opacity = 0.75,
                Margin = new Avalonia.Thickness(0, 6, 0, 0)
            });

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
                InspectorPanel.Children.Add(new TextBlock
                {
                    Text = field.Hint,
                    FontSize = 10,
                    Opacity = 0.45,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap
                });
            }
        }
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
