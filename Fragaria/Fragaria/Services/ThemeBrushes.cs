using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Fragaria.Services;

internal static class ThemeBrushes
{
    private static readonly SolidColorBrush FallbackPrimary = new(Windows.UI.Color.FromArgb(255, 184, 32, 58));
    private static readonly SolidColorBrush FallbackLeaf = new(Windows.UI.Color.FromArgb(255, 52, 199, 90));
    private static readonly SolidColorBrush FallbackText = new(Windows.UI.Color.FromArgb(255, 242, 244, 248));
    private static readonly SolidColorBrush FallbackCaption = new(Windows.UI.Color.FromArgb(255, 196, 202, 210));
    private static readonly SolidColorBrush FallbackMuted = new(Windows.UI.Color.FromArgb(255, 148, 156, 168));

    public static Brush Primary => Get("FragariaPrimaryBrush", FallbackPrimary);
    public static Brush Leaf => Get("FragariaLeafBrush", FallbackLeaf);
    public static Brush Text => Get("FragariaTextBrush", FallbackText);
    public static Brush Caption => Get("FragariaCaptionBrush", FallbackCaption);
    public static Brush Muted => Get("FragariaMutedBrush", FallbackMuted);

    private static Brush Get(string key, Brush fallback)
    {
        try
        {
            if (Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Brush brush)
                return brush;
        }
        catch { }
        return fallback;
    }
}
