using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GameLoopOptimizer.Views.Controls;

public partial class LiveChartControl : UserControl
{
    public static readonly DependencyProperty PointsSourceProperty =
        DependencyProperty.Register(nameof(PointsSource), typeof(INotifyCollectionChanged), typeof(LiveChartControl),
            new PropertyMetadata(null, OnPointsSourceChanged));

    public static readonly DependencyProperty LineBrushProperty =
        DependencyProperty.Register(nameof(LineBrush), typeof(Brush), typeof(LiveChartControl),
            new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0, 229, 255)), OnVisualChanged));

    public static readonly DependencyProperty FillBrushProperty =
        DependencyProperty.Register(nameof(FillBrush), typeof(Brush), typeof(LiveChartControl),
            new PropertyMetadata(null, OnVisualChanged));

    public static readonly DependencyProperty MaxValueProperty =
        DependencyProperty.Register(nameof(MaxValue), typeof(double), typeof(LiveChartControl),
            new PropertyMetadata(100.0, OnVisualChanged));

    public INotifyCollectionChanged? PointsSource
    {
        get => (INotifyCollectionChanged?)GetValue(PointsSourceProperty);
        set => SetValue(PointsSourceProperty, value);
    }

    public Brush LineBrush
    {
        get => (Brush)GetValue(LineBrushProperty);
        set => SetValue(LineBrushProperty, value);
    }

    public Brush? FillBrush
    {
        get => (Brush?)GetValue(FillBrushProperty);
        set => SetValue(FillBrushProperty, value);
    }

    public double MaxValue
    {
        get => (double)GetValue(MaxValueProperty);
        set => SetValue(MaxValueProperty, value);
    }

    public LiveChartControl()
    {
        InitializeComponent();
        UpdateBrushes();
    }

    private static void OnPointsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LiveChartControl ctrl)
        {
            if (e.OldValue is INotifyCollectionChanged oldCol)
            {
                oldCol.CollectionChanged -= ctrl.CollectionChanged;
            }
            if (e.NewValue is INotifyCollectionChanged newCol)
            {
                newCol.CollectionChanged += ctrl.CollectionChanged;
            }
            ctrl.Redraw();
        }
    }

    private static void OnVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LiveChartControl ctrl)
        {
            ctrl.UpdateBrushes();
            ctrl.Redraw();
        }
    }

    private void CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Redraw();
    }

    private void ChartCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        Redraw();
    }

    private void UpdateBrushes()
    {
        ChartLine.Stroke = LineBrush;

        if (FillBrush != null)
        {
            AreaPolygon.Fill = FillBrush;
        }
        else if (LineBrush is SolidColorBrush scb)
        {
            var grad = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1)
            };
            grad.GradientStops.Add(new GradientStop(Color.FromArgb(80, scb.Color.R, scb.Color.G, scb.Color.B), 0));
            grad.GradientStops.Add(new GradientStop(Color.FromArgb(0, scb.Color.R, scb.Color.G, scb.Color.B), 1));
            AreaPolygon.Fill = grad;
        }
    }

    public void Redraw()
    {
        double width = ChartCanvas.ActualWidth;
        double height = ChartCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        // Update grid lines
        GridLineTop.X1 = 0; GridLineTop.Y1 = height * 0.25; GridLineTop.X2 = width; GridLineTop.Y2 = height * 0.25;
        GridLineMid.X1 = 0; GridLineMid.Y1 = height * 0.50; GridLineMid.X2 = width; GridLineMid.Y2 = height * 0.50;
        GridLineBot.X1 = 0; GridLineBot.Y1 = height * 0.75; GridLineBot.X2 = width; GridLineBot.Y2 = height * 0.75;

        if (PointsSource is not IEnumerable<double> source)
        {
            ChartLine.Points.Clear();
            AreaPolygon.Points.Clear();
            return;
        }

        var list = source.ToList();
        if (list.Count < 2) return;

        var linePoints = new PointCollection();
        var polyPoints = new PointCollection();

        double stepX = width / (list.Count - 1);
        double maxVal = Math.Max(1.0, MaxValue);

        polyPoints.Add(new Point(0, height));

        for (int i = 0; i < list.Count; i++)
        {
            double x = i * stepX;
            double normalized = Math.Clamp(list[i] / maxVal, 0.0, 1.0);
            double y = height - (normalized * (height - 6)) - 3;

            var pt = new Point(x, y);
            linePoints.Add(pt);
            polyPoints.Add(pt);
        }

        polyPoints.Add(new Point(width, height));

        ChartLine.Points = linePoints;
        AreaPolygon.Points = polyPoints;
    }
}
