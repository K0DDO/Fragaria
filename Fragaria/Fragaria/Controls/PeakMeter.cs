using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace Fragaria.Controls;

public sealed class PeakMeter : UserControl
{
    private readonly Rectangle _fill;
    private readonly Border _track;

    public PeakMeter()
    {
        _track = new Border
        {
            Width = 8,
            Height = 80,
            CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(40, 255, 255, 255))
        };

        _fill = new Rectangle
        {
            RadiusX = 4,
            RadiusY = 4,
            VerticalAlignment = VerticalAlignment.Bottom,
            Height = 4,
            Fill = (Brush)Application.Current.Resources["FragariaLeafBrush"]
        };

        var grid = new Grid { Width = 8, Height = 80 };
        grid.Children.Add(_track);
        grid.Children.Add(_fill);
        Content = grid;
    }

    public double Level
    {
        get => _fill.Height / 80.0 * 100;
        set
        {
            var h = Math.Clamp(value / 100.0, 0, 1) * 80;
            _fill.Height = Math.Max(4, h);
            _fill.Fill = value > 90
                ? (Brush)Application.Current.Resources["FragariaPrimaryBrush"]
                : (Brush)Application.Current.Resources["FragariaLeafBrush"];
        }
    }
}
