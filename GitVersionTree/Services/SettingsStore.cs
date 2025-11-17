using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace GitVersionTree.Services;

internal static class SettingsStore
{
    private static readonly string SettingsDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppInfo.ProductName);

    private static readonly string SettingsFilePath = Path.Combine(SettingsDirectory, "settings.json");

    private static readonly object SyncRoot = new();

    private static Dictionary<string, string> _values = Load();

    public static string? Read(string name)
    {
        lock (SyncRoot)
        {
            return _values.TryGetValue(name, out var value) ? value : null;
        }
    }

    public static void Write(string name, string value)
    {
        lock (SyncRoot)
        {
            _values[name] = value;
            Persist();
        }
    }

    private static Dictionary<string, string> Load()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                var result = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (result is not null)
                {
                    return result;
                }
            }
        }
        catch
        {
            // Ignore read issues and fall back to an empty store.
        }

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    private static void Persist()
    {
        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            var json = JsonSerializer.Serialize(_values, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFilePath, json);
        }
        catch
        {
            // Persistence errors are swallowed to avoid crashing the UI.
        }
    }
}
