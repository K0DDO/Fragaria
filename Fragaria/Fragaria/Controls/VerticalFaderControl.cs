using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace Fragaria.Controls;

public sealed class VerticalFaderControl : UserControl
{
    private readonly Slider _slider;

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(double), typeof(VerticalFaderControl),
            new PropertyMetadata(75.0, (d, e) => ((VerticalFaderControl)d)._slider.Value = (double)e.NewValue));

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public event RangeBaseValueChangedEventHandler? ValueChanged;

    public VerticalFaderControl()
    {
        _slider = new Slider
        {
            Orientation = Orientation.Vertical,
            Minimum = 0,
            Maximum = 100,
            Height = 140,
            Width = 32,
        };
        _slider.ValueChanged += (s, e) =>
        {
            SetValue(ValueProperty, e.NewValue);
            ValueChanged?.Invoke(s, e);
        };
        Content = _slider;
    }
}
