using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using ScottPlot.WPF;
using Worksheet.Models;

namespace Worksheet.Views.Support
{
    public class ThumbManager
    {
        private const double MinSize = 50;

        public Thumb[] CreateThumbs(Canvas overlay)
        {
            var tl = MakeThumb(Cursors.SizeNWSE);
            var tr = MakeThumb(Cursors.SizeNESW);
            var bl = MakeThumb(Cursors.SizeNESW);
            var br = MakeThumb(Cursors.SizeNWSE);

            overlay.Children.Add(tl);
            overlay.Children.Add(tr);
            overlay.Children.Add(bl);
            overlay.Children.Add(br);

            // Ensure thumbs are above drag layer for hit testing
            Panel.SetZIndex(tl, 10);
            Panel.SetZIndex(tr, 10);
            Panel.SetZIndex(bl, 10);
            Panel.SetZIndex(br, 10);

            // Start hidden (will be shown when selected)
            var thumbs = new[] { tl, tr, bl, br };
            foreach (var thumb in thumbs)
                thumb.Visibility = Visibility.Collapsed;

            return thumbs;
        }

        public void AttachPositioning(WpfPlot plot, Thumb[] thumbs)
        {
            var (tl, tr, bl, br) = (thumbs[0], thumbs[1], thumbs[2], thumbs[3]);

            void UpdateThumbPositions()
            {
                var last = plot.Plot.RenderManager.LastRender;

                // DataRect is pixel coords relative to the plot surface
                var r = last.DataRect;

                // Convert pixels -> WPF DIPs (DPI-aware)
                var dpi = DpiContext.From(plot);
                var dataRectDip = dpi.PixelsToDips(new Rect(r.Left, r.Top, r.Width, r.Height));

                double left = dataRectDip.Left;
                double right = dataRectDip.Right;
                double top = dataRectDip.Top;
                double bottom = dataRectDip.Bottom;

                PlaceThumb(tl, left, top);
                PlaceThumb(tr, right, top);
                PlaceThumb(bl, left, bottom);
                PlaceThumb(br, right, bottom);
            }

            // Update thumbs after every render (handles axis label size changes etc.)
            plot.Plot.RenderManager.RenderFinished += (_, __) =>
            {
                plot.Dispatcher.Invoke(UpdateThumbPositions);
            };

            // Initial update when host is loaded
            var host = plot.Parent as Grid;
            if (host != null)
            {
                host.Loaded += (_, __) =>
                {
                    plot.Refresh();
                    UpdateThumbPositions();
                };
            }
        }

        public void AttachResize(Thumb[] thumbs, PlotContainer container, WpfPlot plot, Func<double>? getSnapSize = null)
        {
            var (tl, tr, bl, br) = (thumbs[0], thumbs[1], thumbs[2], thumbs[3]);

            AttachResizeToThumb(tl, HorizontalAlignment.Left, VerticalAlignment.Top, container, plot, getSnapSize);
            AttachResizeToThumb(tr, HorizontalAlignment.Right, VerticalAlignment.Top, container, plot, getSnapSize);
            AttachResizeToThumb(bl, HorizontalAlignment.Left, VerticalAlignment.Bottom, container, plot, getSnapSize);
            AttachResizeToThumb(br, HorizontalAlignment.Right, VerticalAlignment.Bottom, container, plot, getSnapSize);
        }

        private void AttachResizeToThumb(Thumb thumb, HorizontalAlignment hAlign, VerticalAlignment vAlign,
                                          PlotContainer container, WpfPlot plot, Func<double>? getSnapSize)
        {
            thumb.DragDelta += (_, e) =>
            {
                double newW = container.Host.Width;
                double newH = container.Host.Height;
                double newX = Canvas.GetLeft(container.Container);
                double newY = Canvas.GetTop(container.Container);
                double snapSize = getSnapSize?.Invoke() ?? 0;

                // Horizontal resize
                if (hAlign == HorizontalAlignment.Left)
                {
                    double delta = Math.Min(e.HorizontalChange, container.Host.Width - MinSize);
                    newW = container.Host.Width - delta;
                    newX += delta;
                }
                else if (hAlign == HorizontalAlignment.Right)
                {
                    double delta = Math.Max(e.HorizontalChange, MinSize - container.Host.Width);
                    newW = container.Host.Width + delta;
                }

                // Vertical resize
                if (vAlign == VerticalAlignment.Top)
                {
                    double delta = Math.Min(e.VerticalChange, container.Host.Height - MinSize);
                    newH = container.Host.Height - delta;
                    newY += delta;
                }
                else if (vAlign == VerticalAlignment.Bottom)
                {
                    double delta = Math.Max(e.VerticalChange, MinSize - container.Host.Height);
                    newH = container.Host.Height + delta;
                }

                newW = Math.Max(MinSize, newW);
                newH = Math.Max(MinSize, newH);

                // Snap size and position to grid
                if (snapSize > 0)
                {
                    newW = GridSnap.Snap(newW, snapSize);
                    newH = GridSnap.Snap(newH, snapSize);
                    newX = GridSnap.Snap(newX, snapSize);
                    newY = GridSnap.Snap(newY, snapSize);
                }

                Canvas.SetLeft(container.Container, newX);
                Canvas.SetTop(container.Container, newY);
                container.Host.Width = plot.Width = container.Container.Width = container.DragLayer.Width = newW;
                container.Host.Height = plot.Height = container.Container.Height = container.DragLayer.Height = newH;

                plot.Refresh();
            };
        }

        private static Thumb MakeThumb(Cursor cursor)
        {
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.BackgroundProperty, Brushes.Orange);
            borderFactory.SetValue(Border.BorderBrushProperty, Brushes.Black);
            borderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(0.75));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(0));

            var template = new ControlTemplate(typeof(Thumb));
            template.VisualTree = borderFactory;

            return new Thumb
            {
                Width = 8,
                Height = 8,
                Cursor = cursor,
                Template = template
            };
        }

        private static void PlaceThumb(Thumb t, double xDip, double yDip)
        {
            Canvas.SetLeft(t, xDip - t.Width / 2);
            Canvas.SetTop(t, yDip - t.Height / 2);
        }
    }
}
