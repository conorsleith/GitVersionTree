using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace GitVersionTree.Services;

internal static class SimpleMessageBox
{
    public static Task ShowAsync(Window owner, string title, string message)
    {
        var okButton = new Button
        {
            Content = "OK",
            HorizontalAlignment = HorizontalAlignment.Right,
            MinWidth = 80
        };

        var layout = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 12,
            Children =
            {
                new TextBlock
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 420
                },
                okButton
            }
        };

        var window = new Window
        {
            Title = title,
            SizeToContent = SizeToContent.WidthAndHeight,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = layout
        };

        okButton.Click += (_, _) => window.Close();

        if (owner is not null)
        {
            return window.ShowDialog(owner);
        }

        var tcs = new TaskCompletionSource<object?>();
        window.Closed += (_, _) => tcs.TrySetResult(null);
        window.Show();
        return tcs.Task;
    }
}
