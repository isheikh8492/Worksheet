using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Worksheet.Views.PlotViews.Gates;

namespace Worksheet.Views.Support.Gates
{
    internal enum PreviewKind { Rectangle, Ellipse, Polyline }

    /// <summary>Transient draw-preview shape, in DIP points.</summary>
    internal readonly record struct DrawPreview(PreviewKind Kind, IReadOnlyList<Point> PointsDip);

    /// <summary>
    /// Pure WPF view for gate interaction chrome: the selected gate's draggable handles and the
    /// in-progress draw preview. Holds no gate logic — it renders whatever it is handed.
    /// </summary>
    internal sealed class GateOverlay
    {
        private const double HandleSizeDip = 8;

        private readonly Canvas _layer;
        private readonly PlotCoordinateMapper _mapper;
        private readonly List<Rectangle> _handlePool = new();

        private readonly Rectangle _previewRect;
        private readonly Ellipse _previewEllipse;
        private readonly Polyline _previewPolyline;

        public GateOverlay(Canvas layer, PlotCoordinateMapper mapper)
        {
            _layer = layer;
            _mapper = mapper;

            _previewRect = MakePreviewOutline(new Rectangle());
            _previewEllipse = MakePreviewOutline(new Ellipse());
            _previewPolyline = new Polyline
            {
                Stroke = Brushes.Black,
                StrokeThickness = 1.5,
                StrokeDashArray = new DoubleCollection { 3, 3 },
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false,
            };

            // Previews added first so lazily-created handles render on top.
            _layer.Children.Add(_previewRect);
            _layer.Children.Add(_previewEllipse);
            _layer.Children.Add(_previewPolyline);
        }

        public void ShowHandles(IReadOnlyList<GateHandle> handles)
        {
            EnsurePool(handles.Count);
            for (int i = 0; i < _handlePool.Count; i++)
            {
                var r = _handlePool[i];
                if (i < handles.Count)
                {
                    var dip = _mapper.ToDip(handles[i].Position);
                    Canvas.SetLeft(r, dip.X - HandleSizeDip / 2);
                    Canvas.SetTop(r, dip.Y - HandleSizeDip / 2);
                    r.Visibility = Visibility.Visible;
                }
                else
                {
                    r.Visibility = Visibility.Collapsed;
                }
            }
        }

        public void HideHandles()
        {
            foreach (var r in _handlePool)
                r.Visibility = Visibility.Collapsed;
        }

        public void ShowPreview(DrawPreview preview)
        {
            ClearPreview();
            var pts = preview.PointsDip;
            if (pts.Count == 0)
                return;

            switch (preview.Kind)
            {
                case PreviewKind.Rectangle:
                    PlaceBox(_previewRect, pts);
                    break;
                case PreviewKind.Ellipse:
                    PlaceBox(_previewEllipse, pts);
                    break;
                case PreviewKind.Polyline:
                    _previewPolyline.Points = new PointCollection(pts);
                    _previewPolyline.Visibility = Visibility.Visible;
                    break;
            }
        }

        public void ClearPreview()
        {
            _previewRect.Visibility = Visibility.Collapsed;
            _previewEllipse.Visibility = Visibility.Collapsed;
            _previewPolyline.Visibility = Visibility.Collapsed;
        }

        private static void PlaceBox(FrameworkElement shape, IReadOnlyList<Point> pts)
        {
            double minX = pts[0].X, minY = pts[0].Y, maxX = pts[0].X, maxY = pts[0].Y;
            foreach (var p in pts)
            {
                if (p.X < minX) minX = p.X;
                if (p.X > maxX) maxX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.Y > maxY) maxY = p.Y;
            }
            Canvas.SetLeft(shape, minX);
            Canvas.SetTop(shape, minY);
            shape.Width = System.Math.Max(0, maxX - minX);
            shape.Height = System.Math.Max(0, maxY - minY);
            shape.Visibility = Visibility.Visible;
        }

        private static T MakePreviewOutline<T>(T shape) where T : Shape
        {
            shape.Stroke = Brushes.Black;
            shape.StrokeThickness = 1.5;
            shape.StrokeDashArray = new DoubleCollection { 3, 3 };
            shape.Fill = Brushes.Transparent;
            shape.Visibility = Visibility.Collapsed;
            shape.IsHitTestVisible = false;
            return shape;
        }

        private void EnsurePool(int count)
        {
            while (_handlePool.Count < count)
            {
                var r = new Rectangle
                {
                    Width = HandleSizeDip,
                    Height = HandleSizeDip,
                    Fill = Brushes.Black,
                    Stroke = Brushes.White,
                    StrokeThickness = 1,
                    Visibility = Visibility.Collapsed,
                    IsHitTestVisible = false,
                };
                _handlePool.Add(r);
                _layer.Children.Add(r);
            }
        }
    }
}
