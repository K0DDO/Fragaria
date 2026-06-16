using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

namespace Fragaria.Controls;

public sealed class KnobControl : UserControl
{
    private readonly Slider _slider;
    private readonly TextBlock _label;

    public static readonly DependencyProperty HeaderProperty =
        DependencyProperty.Register(nameof(Header), typeof(string), typeof(KnobControl),
            new PropertyMetadata("", (d, e) => ((KnobControl)d)._label.Text = (string)e.NewValue));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(double), typeof(KnobControl),
            new PropertyMetadata(50.0, (d, e) => ((KnobControl)d)._slider.Value = (double)e.NewValue));

    public string Header
    {
        get => (string)GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public event RangeBaseValueChangedEventHandler? ValueChanged;

    public KnobControl()
    {
        _label = new TextBlock
        {
            Style = (Style)Application.Current.Resources["FragariaLabel"],
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 4)
        };

        var ring = new Border
        {
            Width = 36,
            Height = 36,
            CornerRadius = new CornerRadius(18),
            BorderBrush = (Brush)Application.Current.Resources["FragariaPrimaryBrush"],
            BorderThickness = new Thickness(2),
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 26, 26, 30)),
            HorizontalAlignment = HorizontalAlignment.Center
        };

        _slider = new Slider
        {
            Minimum = 0,
            Maximum = 100,
            Width = 48,
            Style = (Style)Application.Current.Resources["FragariaGlassKnobSlider"]
        };
        _slider.ValueChanged += (s, e) =>
        {
            SetValue(ValueProperty, e.NewValue);
            ValueChanged?.Invoke(s, e);
        };

        var stack = new StackPanel { Spacing = 2, Width = 52 };
        stack.Children.Add(_label);
        var knobHost = new Grid();
        knobHost.Children.Add(ring);
        knobHost.Children.Add(_slider);
        stack.Children.Add(knobHost);
        Content = stack;
    }
}
