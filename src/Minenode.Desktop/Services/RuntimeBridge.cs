using System.Diagnostics;
using System.Text.Json;
using Minenode.Desktop.Models;

namespace Minenode.Desktop.Services;

public sealed class RuntimeBridge : IDisposable
{
    private Process? _process;

    public event Action<string>? OutputReceived;

    public bool IsRunning => _process is { HasExited: false };

    public async Task StartAsync(WorkflowDocument workflow)
    {
        Stop();

        var script = FindRuntimeScript();
        if (script is null)
            throw new FileNotFoundException("runtime/index.mjs could not be found. Run Minenode from the repository or package the runtime beside the application.");

        var profiles = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Minenode",
            "profiles");
        Directory.CreateDirectory(profiles);

        var startInfo = new ProcessStartInfo
        {
            FileName = "node",
            WorkingDirectory = Path.GetDirectoryName(script)!,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add(script);
        startInfo.Environment["MINENODE_PROFILE_DIR"] = profiles;

        _process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        _process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
                OutputReceived?.Invoke(FormatRuntimeLine(e.Data));
        };
        _process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
                OutputReceived?.Invoke(e.Data);
        };
        _process.Exited += (_, _) => OutputReceived?.Invoke("Runtime stopped.");

        if (!_process.Start())
            throw new InvalidOperationException("Unable to start the Node.js runtime.");

        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        var message = JsonSerializer.Serialize(new { type = "run", workflow });
        await _process.StandardInput.WriteLineAsync(message);
        await _process.StandardInput.FlushAsync();
    }

    public void Stop()
    {
        if (_process is null)
            return;

        try
        {
            if (!_process.HasExited)
                _process.Kill(entireProcessTree: true);
        }
        catch
        {
            // The process may already have exited between the state check and kill call.
        }
        finally
        {
            _process.Dispose();
            _process = null;
        }
    }

    private static string? FindRuntimeScript()
    {
        var candidates = new List<string>
        {
            Path.Combine(Directory.GetCurrentDirectory(), "runtime", "index.mjs"),
            Path.Combine(AppContext.BaseDirectory, "runtime", "index.mjs")
        };

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 8 && directory is not null; i++, directory = directory.Parent)
            candidates.Add(Path.Combine(directory.FullName, "runtime", "index.mjs"));

        return candidates.FirstOrDefault(File.Exists);
    }

    private static string FormatRuntimeLine(string line)
    {
        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (root.TryGetProperty("type", out var type) && type.GetString() == "log" &&
                root.TryGetProperty("message", out var message))
            {
                var level = root.TryGetProperty("level", out var levelValue)
                    ? levelValue.GetString() ?? "info"
                    : "info";
                return $"[{level}] {message.GetString()}";
            }
        }
        catch (JsonException)
        {
            // Authentication prompts and third-party logs are intentionally passed through unchanged.
        }

        return line;
    }

    public void Dispose() => Stop();
}
