using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace Fragaria.Controls;

public sealed class VerticalLedMeter : UserControl
{
    private const int SegmentCount = 24;
    private readonly Rectangle[] _segments;
    private readonly Grid _grid;

    public static readonly DependencyProperty LevelProperty =
        DependencyProperty.Register(nameof(Level), typeof(double), typeof(VerticalLedMeter),
            new PropertyMetadata(0.0, (d, _) => ((VerticalLedMeter)d).Redraw()));

    public double Level
    {
        get => (double)GetValue(LevelProperty);
        set => SetValue(LevelProperty, value);
    }

    public VerticalLedMeter()
    {
        _segments = new Rectangle[SegmentCount];
        _grid = new Grid { Width = 10, Height = 140 };

        for (int i = 0; i < SegmentCount; i++)
        {
            var seg = new Rectangle
            {
                Height = 4,
                RadiusX = 1,
                RadiusY = 1,
                Margin = new Thickness(0, 0, 0, 2),
                Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(30, 255, 255, 255))
            };
            _segments[i] = seg;
            _grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(seg, SegmentCount - 1 - i);
            _grid.Children.Add(seg);
        }

        Content = _grid;
    }

    private void Redraw()
    {
        var lit = (int)Math.Round(Math.Clamp(Level, 0, 100) / 100.0 * SegmentCount);
        for (int i = 0; i < SegmentCount; i++)
        {
            var idx = SegmentCount - 1 - i;
            var ratio = (double)(i + 1) / SegmentCount;
            _segments[idx].Fill = i < lit
                ? ratio > 0.85
                    ? (Brush)Application.Current.Resources["FragariaPrimaryBrush"]
                    : ratio > 0.6
                        ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 232, 200, 64))
                        : (Brush)Application.Current.Resources["FragariaLeafBrush"]
                : new SolidColorBrush(Windows.UI.Color.FromArgb(30, 255, 255, 255));
        }
    }
}
