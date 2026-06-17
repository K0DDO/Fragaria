using Fragaria.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Fragaria.Controls;

public sealed class WindowPickerControl : UserControl
{
    public event Action<nint>? WindowSelected;

    private readonly ListView _list;

    public WindowPickerControl()
    {
        _list = new ListView
        {
            SelectionMode = ListViewSelectionMode.Single,
            CanDragItems = true,
            AllowDrop = false,
            MaxHeight = 140,
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 18, 18, 22)),
            CornerRadius = new CornerRadius(6),
            BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(40, 255, 255, 255)),
            BorderThickness = new Thickness(1)
        };
        _list.ItemClick += (_, e) =>
        {
            if (e.ClickedItem is WindowPickerItem item)
                WindowSelected?.Invoke(item.Hwnd);
        };
        _list.DragItemsStarting += (_, e) =>
        {
            if (e.Items.Count > 0 && e.Items[0] is WindowPickerItem item)
            {
                e.Data.SetText(item.Hwnd.ToString());
                e.Data.RequestedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
            }
        };
        Content = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                new TextBlock
                {
                    Text = "Перетащите окно на дорожку",
                    FontFamily = ThemeFonts.UI,
                    Foreground = ThemeBrushes.Caption,
                    FontSize = 12,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap
                },
                _list
            }
        };
    }

    public void SetWindows(IReadOnlyList<Services.WindowInfo> windows)
    {
        _list.Items.Clear();
        foreach (var w in windows)
            _list.Items.Add(new WindowPickerItem(w.Hwnd, w.Title, w.ProcessName));
    }
}

internal sealed class WindowPickerItem
{
    public nint Hwnd { get; }
    public string Title { get; }
    public string Sub { get; }
    public WindowPickerItem(nint hwnd, string title, string sub)
    { Hwnd = hwnd; Title = title; Sub = sub; }
    public override string ToString() => $"{Title} ({Sub})";
}
