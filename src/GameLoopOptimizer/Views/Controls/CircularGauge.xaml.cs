using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GameLoopOptimizer.Views.Controls;

public partial class CircularGauge : UserControl
{
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(double), typeof(CircularGauge),
            new PropertyMetadata(0.0, OnValueChanged));

    public static readonly DependencyProperty MaxValueProperty =
        DependencyProperty.Register(nameof(MaxValue), typeof(double), typeof(CircularGauge),
            new PropertyMetadata(100.0, OnValueChanged));

    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(CircularGauge),
            new PropertyMetadata("OPTIMIZATION SCORE", (d, e) =>
            {
                if (d is CircularGauge g && e.NewValue is string s) g.UnitText.Text = s;
            }));

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double MaxValue
    {
        get => (double)GetValue(MaxValueProperty);
        set => SetValue(MaxValueProperty, value);
    }

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public CircularGauge()
    {
        InitializeComponent();
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CircularGauge gauge)
        {
            gauge.UpdateGauge();
        }
    }

    private void Grid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateGauge();
    }

    private void UpdateGauge()
    {
        double size = Math.Min(ActualWidth, ActualHeight);
        if (size <= 20) return;

        double radius = (size / 2) - 14;
        double cx = 0;
        double cy = 0;

        ValueText.Text = Math.Round(Value).ToString();

        // 270 degree arc from 135 deg to 405 deg
        double startAngle = 135;
        double totalAngle = 270;
        double currentAngle = startAngle + (Math.Clamp(Value / Math.Max(1, MaxValue), 0.0, 1.0) * totalAngle);

        TrackPath.Data = CreateArcGeometry(cx, cy, radius, startAngle, startAngle + totalAngle);
        ValuePath.Data = CreateArcGeometry(cx, cy, radius, startAngle, currentAngle);
    }

    private static PathGeometry CreateArcGeometry(double cx, double cy, double r, double startAngle, double endAngle)
    {
        if (endAngle <= startAngle) endAngle = startAngle + 0.1;

        double startRad = (Math.PI / 180.0) * startAngle;
        double endRad = (Math.PI / 180.0) * endAngle;

        Point startPt = new Point(cx + r * Math.Cos(startRad), cy + r * Math.Sin(startRad));
        Point endPt = new Point(cx + r * Math.Cos(endRad), cy + r * Math.Sin(endRad));

        bool isLargeArc = (endAngle - startAngle) > 180.0;

        var figure = new PathFigure
        {
            StartPoint = startPt,
            IsClosed = false
        };
        figure.Segments.Add(new ArcSegment(endPt, new Size(r, r), 0, isLargeArc, SweepDirection.Clockwise, true));

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }
}
