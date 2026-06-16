using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace Fragaria.Controls;

public sealed class MiniWaveform : UserControl
{
    private readonly Canvas _canvas;
    private readonly Polyline _line;
    private readonly float[] _bands = new float[32];

    public MiniWaveform()
    {
        _line = new Polyline
        {
            Stroke = (Brush)Application.Current.Resources["FragariaLeafBrush"],
            StrokeThickness = 1.5,
            Fill = null
        };

        _canvas = new Canvas { Width = 88, Height = 28, Background = new SolidColorBrush(Windows.UI.Color.FromArgb(40, 0, 0, 0)) };
        _canvas.Children.Add(_line);
        Content = _canvas;
    }

    public void SetBands(float[] bands)
    {
        if (bands.Length < 8) return;
        Array.Copy(bands, _bands, Math.Min(bands.Length, 32));
        var points = new PointCollection();
        var step = 88.0 / 16;
        for (int i = 0; i < 16; i++)
        {
            var v = Math.Clamp(_bands[i * 2] * 80, 0, 26);
            points.Add(new Windows.Foundation.Point(i * step, 26 - v));
        }
        _line.Points = points;
    }
}
