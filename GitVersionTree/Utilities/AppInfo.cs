using System.Reflection;

namespace GitVersionTree;

internal static class AppInfo
{
    private static readonly Assembly EntryAssembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();

    public static string ProductName { get; } = EntryAssembly.GetName().Name ?? "GitVersionTree";

    public static string ProductVersion { get; } =
        EntryAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? EntryAssembly.GetName().Version?.ToString() ?? "2.0.1-alpha";
}
