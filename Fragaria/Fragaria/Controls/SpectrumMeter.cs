using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace Fragaria.Controls;

public sealed class SpectrumMeter : UserControl
{
    private readonly Canvas _canvas;
    private readonly Rectangle[] _bars;
    private const int BandCount = 32;

    public SpectrumMeter()
    {
        _canvas = new Canvas { Width = 88, Height = 28 };
        _bars = new Rectangle[BandCount];
        for (int i = 0; i < BandCount; i++)
        {
            var bar = new Rectangle
            {
                Width = 2.5,
                RadiusX = 1, RadiusY = 1,
                Fill = (Brush)Application.Current.Resources["FragariaPrimaryBrush"]
            };
            _bars[i] = bar;
            _canvas.Children.Add(bar);
        }
        Content = _canvas;
    }

    public void SetBands(float[] bands)
    {
        if (bands.Length < BandCount) return;
        for (int i = 0; i < BandCount; i++)
        {
            var h = Math.Clamp(bands[i] * 400, 2, 26);
            Canvas.SetLeft(_bars[i], i * 2.7);
            Canvas.SetTop(_bars[i], 28 - h);
            _bars[i].Height = h;
            _bars[i].Fill = h > 22
                ? (Brush)Application.Current.Resources["FragariaPrimaryBrush"]
                : (Brush)Application.Current.Resources["FragariaLeafBrush"];
        }
    }
}
