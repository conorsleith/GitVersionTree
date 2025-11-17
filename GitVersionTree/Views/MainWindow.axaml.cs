using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using GitVersionTree.Services;
using System.Linq;

namespace GitVersionTree.Views;

public partial class MainWindow : Window
{
    private readonly Dictionary<string, string> _decorateDictionary = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<List<string>> _nodes = new();
    private readonly StringBuilder _statusBuilder = new();
    private bool _isUpdatingPaths;

    private TextBox? _gitPathTextBox;
    private TextBox? _graphvizPathTextBox;
    private TextBox? _repositoryPathTextBox;
    private TextBox? _statusTextBox;
    private Button? _generateButton;

    public MainWindow()
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif
        _gitPathTextBox = this.FindControl<TextBox>("GitPathTextBox");
        _graphvizPathTextBox = this.FindControl<TextBox>("GraphvizDotPathTextBox");
        _repositoryPathTextBox = this.FindControl<TextBox>("GitRepositoryPathTextBox");
        _statusTextBox = this.FindControl<TextBox>("StatusTextBox");
        _generateButton = this.FindControl<Button>("GenerateButton");

        if (_gitPathTextBox is not null)
        {
            _gitPathTextBox.TextChanged += (_, _) => PersistPathSetting("GitPath", _gitPathTextBox.Text);
        }

        if (_graphvizPathTextBox is not null)
        {
            _graphvizPathTextBox.TextChanged += (_, _) => PersistPathSetting("GraphvizPath", _graphvizPathTextBox.Text);
        }

        if (_repositoryPathTextBox is not null)
        {
            _repositoryPathTextBox.TextChanged += (_, _) => PersistPathSetting("GitRepositoryPath", _repositoryPathTextBox.Text);
        }

        Title = $"{AppInfo.ProductName} - v{AppInfo.ProductVersion}";
        RefreshPath();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void RefreshPath()
    {
        _isUpdatingPaths = true;

        if (_gitPathTextBox is not null)
        {
            _gitPathTextBox.Text = SettingsStore.Read("GitPath") ?? string.Empty;
        }

        if (_graphvizPathTextBox is not null)
        {
            _graphvizPathTextBox.Text = SettingsStore.Read("GraphvizPath") ?? string.Empty;
        }

        if (_repositoryPathTextBox is not null)
        {
            _repositoryPathTextBox.Text = SettingsStore.Read("GitRepositoryPath") ?? string.Empty;
        }

        _isUpdatingPaths = false;
    }

    private async void OnGitPathBrowseClicked(object? sender, RoutedEventArgs e)
    {
        var selection = await PickFileAsync(SettingsStore.Read("GitPath"), "Select git executable", OperatingSystem.IsWindows() ? new[] { "exe" } : Array.Empty<string>());
        if (!string.IsNullOrWhiteSpace(selection))
        {
            SettingsStore.Write("GitPath", selection);
            RefreshPath();
        }
    }

    private async void OnGraphvizPathBrowseClicked(object? sender, RoutedEventArgs e)
    {
        var selection = await PickFileAsync(SettingsStore.Read("GraphvizPath"), "Select Graphviz dot executable", OperatingSystem.IsWindows() ? new[] { "exe" } : Array.Empty<string>());
        if (!string.IsNullOrWhiteSpace(selection))
        {
            SettingsStore.Write("GraphvizPath", selection);
            RefreshPath();
        }
    }

    private async void OnRepositoryBrowseClicked(object? sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Git repository"
        };

        var current = SettingsStore.Read("GitRepositoryPath");
        if (!string.IsNullOrWhiteSpace(current) && Directory.Exists(current))
        {
            dialog.Directory = current;
        }

        var result = await dialog.ShowAsync(this);
        if (!string.IsNullOrWhiteSpace(result))
        {
            SettingsStore.Write("GitRepositoryPath", result);
            RefreshPath();
        }
    }

    private async void OnGenerateClicked(object? sender, RoutedEventArgs e)
    {
        await GenerateAsync();
    }

    private void OnExitClicked(object? sender, RoutedEventArgs e) => Close();

    private void OnHomepageLinkClicked(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
        const string homepage = "https://github.com/crc8/GitVersionTree";
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = homepage,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            AppendStatus($"Unable to open homepage: {ex.Message}");
        }
    }

    private async Task GenerateAsync()
    {
        var gitPath = SettingsStore.Read("GitPath");
        var graphvizPath = SettingsStore.Read("GraphvizPath");
        var repositoryPath = SettingsStore.Read("GitRepositoryPath");

        if (string.IsNullOrWhiteSpace(gitPath) ||
            string.IsNullOrWhiteSpace(graphvizPath) ||
            string.IsNullOrWhiteSpace(repositoryPath))
        {
            await SimpleMessageBox.ShowAsync(this, "Generate", "Please select a Git executable, Graphviz dot executable and Git repository.");
            return;
        }

        if (!File.Exists(gitPath) || !File.Exists(graphvizPath) || !Directory.Exists(repositoryPath))
        {
            await SimpleMessageBox.ShowAsync(this, "Generate", "One or more configured paths could not be found.");
            return;
        }

        _statusBuilder.Clear();
        AppendStatus("Starting generation ...");
        SetBusy(true);
        try
        {
            await Task.Run(() => GenerateVersionTree(gitPath, graphvizPath, repositoryPath));
        }
        catch (Exception ex)
        {
            AppendStatus($"Unexpected error: {ex.Message}");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool isBusy)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_generateButton is not null)
            {
                _generateButton.IsEnabled = !isBusy;
            }
        });
    }

    private void AppendStatus(string message)
    {
        var line = $"{DateTime.Now:G} - {message}{Environment.NewLine}";
        _statusBuilder.Append(line);

        Dispatcher.UIThread.Post(() =>
        {
            if (_statusTextBox is not null)
            {
                _statusTextBox.Text = _statusBuilder.ToString();
                _statusTextBox.CaretIndex = _statusTextBox.Text.Length;
            }
        });
    }

    private async Task<string?> PickFileAsync(string? currentValue, string title, IReadOnlyCollection<string> extensions)
    {
        var dialog = new OpenFileDialog
        {
            AllowMultiple = false,
            Title = title
        };

        if (!string.IsNullOrWhiteSpace(currentValue))
        {
            var directory = Path.GetDirectoryName(currentValue);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                dialog.Directory = directory;
            }
        }

        if (extensions.Count > 0)
        {
            dialog.Filters.Add(new FileDialogFilter
            {
                Name = title,
                Extensions = extensions.Select(ext => ext.TrimStart('.')).ToList()
            });
        }

        var result = await dialog.ShowAsync(this);
        return result is { Length: > 0 } ? result[0] : null;
    }

    private void GenerateVersionTree(string gitPath, string graphvizPath, string repositoryPath)
    {
        _decorateDictionary.Clear();
        _nodes.Clear();

        var repositoryName = new DirectoryInfo(repositoryPath).Name;
        var baseDirectory = AppContext.BaseDirectory ?? Environment.CurrentDirectory;
        var dotFilename = Path.Combine(baseDirectory, $"{repositoryName}.dot");
        var pdfFilename = Path.Combine(baseDirectory, $"{repositoryName}.pdf");
        var psFilename = Path.ChangeExtension(pdfFilename, ".ps");
        var logFilename = Path.Combine(baseDirectory, $"{repositoryName}.log");
        File.WriteAllText(logFilename, string.Empty);

        var gitDir = Path.Combine(repositoryPath, ".git");
        AppendStatus("Getting git commit(s) ...");
        var commitResult = Execute(gitPath, $"--git-dir \"{gitDir}\" log --all --pretty=format:\"%h|%p|%d\"");
        if (string.IsNullOrWhiteSpace(commitResult))
        {
            AppendStatus("Unable to get branch or branch empty ...");
        }
        else
        {
            File.AppendAllText(logFilename, "[commit(s)]" + Environment.NewLine);
            File.AppendAllText(logFilename, commitResult + Environment.NewLine);
            foreach (var line in SplitLines(commitResult))
            {
                var columns = line.Split('|');
                if (columns.Length >= 3 && !string.IsNullOrWhiteSpace(columns[2]))
                {
                    var key = columns[0].Trim();
                    if (!_decorateDictionary.ContainsKey(key))
                    {
                        _decorateDictionary.Add(key, columns[2]);
                    }
                }
            }

            AppendStatus($"Processed {_decorateDictionary.Count} decorate(s) ...");
        }

        AppendStatus("Getting git ref branch(es) ...");
        var refResult = Execute(gitPath, $"--git-dir \"{gitDir}\" for-each-ref --format=\"%(objectname:short)|%(refname:short)\"");
        if (string.IsNullOrWhiteSpace(refResult))
        {
            AppendStatus("Unable to get branch or branch empty ...");
        }
        else
        {
            File.AppendAllText(logFilename, "[ref branch(es)]" + Environment.NewLine);
            File.AppendAllText(logFilename, refResult + Environment.NewLine);
            var refLines = SplitLines(refResult);
            ProcessRefs(refLines, gitPath, gitDir, masterOnly: true);
            ProcessRefs(refLines, gitPath, gitDir, masterOnly: false);
        }

        AppendStatus("Getting git merged branch(es) ...");
        var mergedResult = Execute(gitPath, $"--git-dir \"{gitDir}\" log --all --merges --pretty=format:\"%h|%p\"");
        if (string.IsNullOrWhiteSpace(mergedResult))
        {
            AppendStatus("Unable to get merged branch or branch empty ...");
        }
        else
        {
            File.AppendAllText(logFilename, "[merged branch(es)]" + Environment.NewLine);
            File.AppendAllText(logFilename, mergedResult + Environment.NewLine);
            foreach (var line in SplitLines(mergedResult))
            {
                var columns = line.Split('|');
                if (columns.Length < 2)
                {
                    continue;
                }

                var mergedParents = columns[1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (mergedParents.Length <= 1)
                {
                    continue;
                }

                for (var i = 1; i < mergedParents.Length; i++)
                {
                    var parentResult = Execute(gitPath, $"--git-dir \"{gitDir}\" log --reverse --first-parent --pretty=format:\"%h\" {mergedParents[i]}");
                    if (string.IsNullOrWhiteSpace(parentResult))
                    {
                        AppendStatus("Unable to get commit(s) ...");
                        continue;
                    }

                    var branchLines = SplitLines(parentResult);
                    if (branchLines.Length == 0)
                    {
                        continue;
                    }

                    var branch = new List<string>(branchLines)
                    {
                        columns[0]
                    };
                    _nodes.Add(branch);
                }
            }
        }

        AppendStatus($"Processed {_nodes.Count} branch(es) ...");
        AppendStatus("Generating dot file ...");

        var dotBuilder = new StringBuilder();
        dotBuilder.AppendLine($"strict digraph \"{repositoryName}\" {{");
        for (var i = 0; i < _nodes.Count; i++)
        {
            dotBuilder.AppendLine($"  node[group=\"{i + 1}\"];");
            var branch = _nodes[i];
            dotBuilder.Append("  ");
            for (var j = 0; j < branch.Count; j++)
            {
                dotBuilder.Append($"\"{branch[j]}\"");
                dotBuilder.Append(j < branch.Count - 1 ? " -> " : ";");
            }

            dotBuilder.AppendLine();
        }

        var decorateCount = 0;
        foreach (var kvp in _decorateDictionary)
        {
            decorateCount++;
            var label = kvp.Value.Trim();
            dotBuilder.AppendLine($"  subgraph Decorate{decorateCount}");
            dotBuilder.AppendLine("  {");
            dotBuilder.AppendLine("    rank=\"same\";");
            if (label.StartsWith("(tag:", StringComparison.OrdinalIgnoreCase))
            {
                dotBuilder.AppendLine($"    \"{label}\" [shape=\"box\", style=\"filled\", fillcolor=\"#ffffdd\"];");
            }
            else
            {
                dotBuilder.AppendLine($"    \"{label}\" [shape=\"box\", style=\"filled\", fillcolor=\"#ddddff\"];");
            }

            dotBuilder.AppendLine($"    \"{label}\" -> \"{kvp.Key}\" [weight=0, arrowhead=\"none\", style=\"dotted\"];");
            dotBuilder.AppendLine("  }");
        }

        dotBuilder.AppendLine("}");
        File.WriteAllText(dotFilename, dotBuilder.ToString());

        AppendStatus("Generating version tree ...");
        var pdfExitCode = RunDot(graphvizPath, dotFilename, "-Tpdf -Gsize=10,10", pdfFilename);
        var psExitCode = RunDot(graphvizPath, dotFilename, "-Tps", psFilename);

        if (pdfExitCode == 0 && psExitCode == 0 && File.Exists(pdfFilename))
        {
            AppendStatus($"Version tree generated at {pdfFilename}");
        }
        else
        {
            AppendStatus("Version tree generation failed ...");
        }

        AppendStatus("Done!");
    }

    private void ProcessRefs(IEnumerable<string> lines, string gitPath, string gitDir, bool masterOnly)
    {
        foreach (var line in lines)
        {
            var columns = line.Split('|');
            if (columns.Length < 2)
            {
                continue;
            }

            var refName = columns[1];
            if (refName.StartsWith("refs/tags", StringComparison.OrdinalIgnoreCase) ||
                refName.StartsWith("tags/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var isMaster = refName.Contains("master", StringComparison.OrdinalIgnoreCase);
            if (masterOnly && !isMaster)
            {
                continue;
            }

            if (!masterOnly && isMaster)
            {
                continue;
            }

            var result = Execute(gitPath, $"--git-dir \"{gitDir}\" log --reverse --first-parent --pretty=format:\"%h\" {columns[0]}");
            if (string.IsNullOrWhiteSpace(result))
            {
                AppendStatus("Unable to get commit(s) ...");
                continue;
            }

            var branchLines = SplitLines(result);
            if (branchLines.Length == 0)
            {
                continue;
            }

            _nodes.Add(new List<string>(branchLines));
        }
    }

    private static string Execute(string command, string arguments)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0 ? output : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string[] SplitLines(string value) =>
        value.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

    private static int RunDot(string graphvizPath, string dotFilename, string options, string outputFile)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = graphvizPath,
                    Arguments = $"\"{dotFilename}\" {options} -o\"{outputFile}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            process.WaitForExit();
            return process.ExitCode;
        }
        catch
        {
            return -1;
        }
    }

    private void PersistPathSetting(string key, string? value)
    {
        if (_isUpdatingPaths)
        {
            return;
        }

        SettingsStore.Write(key, value ?? string.Empty);
    }
}
