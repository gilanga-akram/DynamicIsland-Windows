// Language: C#
// Filename: CircularProgress.cs
// Runs on: Windows 10 / Windows 11 (.NET 9.0-windows10.0.19041.0)
// Targets: Dynamic Apple-style circular battery ring drawing

using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DynamicIsland
{
    public class CircularProgress : Shape
    {
        public static readonly DependencyProperty PercentageProperty =
            DependencyProperty.Register(
                nameof(Percentage),
                typeof(double),
                typeof(CircularProgress),
                new FrameworkPropertyMetadata(85.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public double Percentage
        {
            get => (double)GetValue(PercentageProperty);
            set => SetValue(PercentageProperty, value);
        }

        protected override Geometry DefiningGeometry => CreateArcGeometry();

        private Geometry CreateArcGeometry()
        {
            double stroke = StrokeThickness;
            double w = Math.Max(0, ActualWidth - stroke);
            double h = Math.Max(0, ActualHeight - stroke);
            double radius = Math.Min(w, h) / 2.0;

            if (radius <= 0) return Geometry.Empty;

            double cx = ActualWidth / 2.0;
            double cy = ActualHeight / 2.0;

            double pct = Math.Clamp(Percentage, 0.0, 100.0);
            if (pct >= 99.5)
            {
                // Full complete circle
                return new EllipseGeometry(new Point(cx, cy), radius, radius);
            }

            if (pct <= 0.5) return Geometry.Empty;

            double angle = (pct / 100.0) * 360.0;
            double rad = (angle - 90.0) * Math.PI / 180.0;

            // Start at top (12 o'clock)
            Point startPoint = new Point(cx, cy - radius);

            // End point along arc
            double endX = cx + radius * Math.Cos(rad);
            double endY = cy + radius * Math.Sin(rad);
            Point endPoint = new Point(endX, endY);

            bool isLargeArc = angle > 180.0;

            var streamGeom = new StreamGeometry();
            using (var ctx = streamGeom.Open())
            {
                ctx.BeginFigure(startPoint, false, false);
                ctx.ArcTo(endPoint, new Size(radius, radius), 0, isLargeArc, SweepDirection.Clockwise, true, false);
            }

            streamGeom.Freeze();
            return streamGeom;
        }
    }
}
