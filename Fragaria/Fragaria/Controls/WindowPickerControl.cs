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
            AllowDrop = false
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
            Spacing = 8,
            Children =
            {
                new TextBlock
                {
                    Text = "Перетащи окно на дорожку или кликни",
                    Foreground = ThemeBrushes.Muted,
                    FontSize = 12
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
