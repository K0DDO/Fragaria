using Fragaria.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace Fragaria.Controls;

public sealed class KnobControl : UserControl
{
    private readonly Slider _slider;
    private readonly TextBlock _label;
    private readonly Ellipse _indicator;
    private readonly Border _ring;

    public static readonly DependencyProperty HeaderProperty =
        DependencyProperty.Register(nameof(Header), typeof(string), typeof(KnobControl),
            new PropertyMetadata("", (d, e) => ((KnobControl)d)._label.Text = (string)e.NewValue));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(double), typeof(KnobControl),
            new PropertyMetadata(50.0, (d, e) =>
            {
                var c = (KnobControl)d;
                if (Math.Abs(c._slider.Value - (double)e.NewValue) > 0.01)
                    c._slider.Value = (double)e.NewValue;
                c.UpdateIndicator();
            }));

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
            FontSize = 8,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = ThemeBrushes.Muted,
            HorizontalAlignment = HorizontalAlignment.Center,
            CharacterSpacing = 80
        };

        _indicator = new Ellipse
        {
            Width = 6,
            Height = 6,
            Fill = ThemeBrushes.Leaf,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 3, 0, 0)
        };

        _ring = new Border
        {
            Width = 34,
            Height = 34,
            CornerRadius = new CornerRadius(17),
            BorderBrush = ThemeBrushes.Primary,
            BorderThickness = new Thickness(1.5),
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 22, 22, 26)),
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var ringGrid = new Grid();
        ringGrid.Children.Add(_ring);
        ringGrid.Children.Add(_indicator);

        _slider = new Slider
        {
            Minimum = 0,
            Maximum = 100,
            Width = 40,
            Height = 8,
            Opacity = 0.02,
            VerticalAlignment = VerticalAlignment.Center
        };
        _slider.Loaded += (_, _) =>
        {
            if (Application.Current.Resources.TryGetValue("FragariaGlassKnobSlider", out var style) && style is Style s)
                _slider.Style = s;
        };
        _slider.ValueChanged += (s, e) =>
        {
            SetValue(ValueProperty, e.NewValue);
            UpdateIndicator();
            ValueChanged?.Invoke(s, e);
        };

        var knobHost = new Grid { Width = 40, Height = 40 };
        knobHost.Children.Add(ringGrid);
        knobHost.Children.Add(_slider);

        var stack = new StackPanel { Spacing = 3, Width = 44 };
        stack.Children.Add(_label);
        stack.Children.Add(knobHost);
        Content = stack;
    }

    private void UpdateIndicator()
    {
        var angle = Value / 100.0 * 270 - 135;
        var rad = angle * Math.PI / 180;
        var r = 12.0;
        _indicator.RenderTransform = new TranslateTransform
        {
            X = Math.Sin(rad) * r,
            Y = -Math.Cos(rad) * r + 17
        };
    }
}
