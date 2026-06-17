using Fragaria.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace Fragaria.Controls;

public sealed class VerticalFaderControl : UserControl
{
    private readonly Slider _slider;
    private readonly TextBlock _label;

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(double), typeof(VerticalFaderControl),
            new PropertyMetadata(75.0, (d, e) =>
            {
                var c = (VerticalFaderControl)d;
                if (Math.Abs(c._slider.Value - (double)e.NewValue) > 0.01)
                    c._slider.Value = (double)e.NewValue;
            }));

    public static readonly DependencyProperty HeaderProperty =
        DependencyProperty.Register(nameof(Header), typeof(string), typeof(VerticalFaderControl),
            new PropertyMetadata("A2", (d, e) => ((VerticalFaderControl)d)._label.Text = (string)e.NewValue));

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string Header
    {
        get => (string)GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public event RangeBaseValueChangedEventHandler? ValueChanged;

    public VerticalFaderControl()
    {
        _label = new TextBlock
        {
            FontSize = 9,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = ThemeBrushes.Muted,
            HorizontalAlignment = HorizontalAlignment.Center,
            CharacterSpacing = 60
        };

        _slider = new Slider
        {
            Orientation = Orientation.Vertical,
            Minimum = 0,
            Maximum = 100,
            Height = 156,
            Width = 36,
        };

        _slider.Loaded += (_, _) =>
        {
            if (Application.Current.Resources.TryGetValue("FragariaVerticalFader", out var style) && style is Style s)
                _slider.Style = s;
        };

        _slider.ValueChanged += (s, e) =>
        {
            SetValue(ValueProperty, e.NewValue);
            ValueChanged?.Invoke(s, e);
        };

        var stack = new StackPanel { Spacing = 4, HorizontalAlignment = HorizontalAlignment.Center };
        stack.Children.Add(_label);
        stack.Children.Add(_slider);
        Content = stack;
    }
}
