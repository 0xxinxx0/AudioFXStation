using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;

namespace AudioFXStation;

public partial class EqualizerWindow : Window
{
    private readonly EqualizerSampleProvider? equalizer;

    public EqualizerWindow(EqualizerSampleProvider? equalizer)
    {
        InitializeComponent();
        this.equalizer = equalizer;

        if (equalizer != null)
        {
            Band1.Value = equalizer.SubGain;
            Band2.Value = equalizer.BassGain;
            Band3.Value = equalizer.MidGain;
            Band4.Value = equalizer.HighMidGain;
            Band5.Value = equalizer.TrebleGain;
        }

        Band1.ValueChanged += (_, _) => UpdateBands();
        Band2.ValueChanged += (_, _) => UpdateBands();
        Band3.ValueChanged += (_, _) => UpdateBands();
        Band4.ValueChanged += (_, _) => UpdateBands();
        Band5.ValueChanged += (_, _) => UpdateBands();

        GraphCanvas.SizeChanged += (_, _) => DrawGraph();

        UpdateBands();
    }

    private void UpdateBands()
    {
        equalizer?.SetFiveGains(
            (float)Band1.Value,
            (float)Band2.Value,
            (float)Band3.Value,
            (float)Band4.Value,
            (float)Band5.Value);

        DrawGraph();
    }

    private void DrawGraph()
    {
        double width = GraphCanvas.Bounds.Width;
        double height = GraphCanvas.Bounds.Height;

        if (width <= 70 || height <= 60)
            return;

        GraphCanvas.Children.Clear();

        double plotLeft = 38;
        double plotRight = width - 14;
        double plotTop = 14;
        double plotBottom = height - 23;
        double plotHeight = plotBottom - plotTop;
        double center = plotTop + plotHeight / 2;

        var gridBrush = new SolidColorBrush(Color.Parse("#344F5C58"));
        var axisBrush = new SolidColorBrush(Color.Parse("#66788982"));

        foreach (int db in new[] { 12, 6, 0, -6, -12 })
        {
            double y = center - db * plotHeight / 30;
            GraphCanvas.Children.Add(new Line
            {
                StartPoint = new Point(plotLeft, y),
                EndPoint = new Point(plotRight, y),
                Stroke = db == 0 ? axisBrush : gridBrush,
                StrokeThickness = db == 0 ? 1.2 : 1
            });

            GraphCanvas.Children.Add(new TextBlock
            {
                Text = db > 0 ? $"+{db}" : db.ToString(),
                Foreground = new SolidColorBrush(Color.Parse("#8EADB1B0")),
                FontSize = 9,
                [Canvas.LeftProperty] = 7,
                [Canvas.TopProperty] = y - 7
            });
        }

        double[] values =
        {
            Band1.Value,
            Band2.Value,
            Band3.Value,
            Band4.Value,
            Band5.Value
        };

        string[] frequencies = { "60", "250", "1k", "4k", "10k" };
        double step = (plotRight - plotLeft) / 4;

        var points = new Point[5];

        for (int i = 0; i < 5; i++)
        {
            double x = plotLeft + i * step;
            double y = center - values[i] * plotHeight / 30;
            points[i] = new Point(
                x,
                y);

            GraphCanvas.Children.Add(new Line
            {
                StartPoint = new Point(x, plotTop),
                EndPoint = new Point(x, plotBottom),
                Stroke = gridBrush,
                StrokeThickness = 1
            });

            GraphCanvas.Children.Add(new TextBlock
            {
                Text = frequencies[i],
                Foreground = new SolidColorBrush(Color.Parse("#A9C0BE")),
                FontSize = 9,
                [Canvas.LeftProperty] = x - (i == 4 ? 19 : 9),
                [Canvas.TopProperty] = plotBottom + 5
            });
        }

        var curvePoints = new List<Point>();
        for (int i = 0; i < points.Length - 1; i++)
        {
            Point p0 = points[Math.Max(0, i - 1)];
            Point p1 = points[i];
            Point p2 = points[i + 1];
            Point p3 = points[Math.Min(points.Length - 1, i + 2)];

            for (int sample = 0; sample < 16; sample++)
            {
                curvePoints.Add(CatmullRom(p0, p1, p2, p3, sample / 16.0));
            }
        }
        curvePoints.Add(points[^1]);

        var glowBrush = new SolidColorBrush(Color.Parse("#556A5CFF"));
        var curveBrush = new SolidColorBrush(Color.Parse("#D98BFF"));
        for (int i = 0; i < curvePoints.Count - 1; i++)
        {
            GraphCanvas.Children.Add(new Line
            {
                StartPoint = curvePoints[i],
                EndPoint = curvePoints[i + 1],
                Stroke = glowBrush,
                StrokeThickness = 10
            });
        }

        var tealBrush = new SolidColorBrush(Color.Parse("#62E6D0"));
        for (int i = 0; i < curvePoints.Count - 1; i++)
        {
            GraphCanvas.Children.Add(new Line
            {
                StartPoint = curvePoints[i],
                EndPoint = curvePoints[i + 1],
                Stroke = curveBrush,
                StrokeThickness = 3
            });
        }

        foreach (var point in points)
        {
            GraphCanvas.Children.Add(new Ellipse
            {
                Width = 14,
                Height = 14,
                Fill = new SolidColorBrush(Color.Parse("#D9182026")),
                Stroke = tealBrush,
                StrokeThickness = 2,
                [Canvas.LeftProperty] = point.X - 7,
                [Canvas.TopProperty] = point.Y - 7
            });
            GraphCanvas.Children.Add(new Ellipse
            {
                Width = 5,
                Height = 5,
                Fill = tealBrush,
                [Canvas.LeftProperty] = point.X - 2.5,
                [Canvas.TopProperty] = point.Y - 2.5
            });
        }
    }

    private static Point CatmullRom(Point p0, Point p1, Point p2, Point p3, double t)
    {
        double t2 = t * t;
        double t3 = t2 * t;

        double x = 0.5 * ((2 * p1.X) + (-p0.X + p2.X) * t +
                          (2 * p0.X - 5 * p1.X + 4 * p2.X - p3.X) * t2 +
                          (-p0.X + 3 * p1.X - 3 * p2.X + p3.X) * t3);
        double y = 0.5 * ((2 * p1.Y) + (-p0.Y + p2.Y) * t +
                          (2 * p0.Y - 5 * p1.Y + 4 * p2.Y - p3.Y) * t2 +
                          (-p0.Y + 3 * p1.Y - 3 * p2.Y + p3.Y) * t3);

        return new Point(x, y);
    }
}