using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ScottPlot.WPF;
using Worksheet.Core.Models;
using Worksheet.App.Views.Surfaces;

using Worksheet.App.Models;
namespace Worksheet.App.Views.Support
{
    public class PlotContainerFactory
    {
        private const double Margin = 10;

        public PlotContainer CreateContainer(WpfPlot plot, int childIndex, double worksheetWidth)
        {
            var plotBacking = new Border
            {
                Background = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false
            };

            var dynamicSurface = new DynamicBitmap
            {
                Width = plot.Width,
                Height = plot.Height
            };

            // Overlay canvas for thumbs and drag layer
            var overlay = new Canvas
            {
                Background = Brushes.Transparent,
                IsHitTestVisible = true,
            };

            // Host grid that holds plot + overlay
            var host = new Grid
            {
                Width = plot.Width,
                Height = plot.Height
            };
            host.Children.Add(plotBacking);
            host.Children.Add(dynamicSurface);
            host.Children.Add(plot);
            host.Children.Add(overlay);

            Panel.SetZIndex(plotBacking, 0);
            Panel.SetZIndex(dynamicSurface, 1);
            Panel.SetZIndex(plot, 2);
            Panel.SetZIndex(overlay, 3);

            // Outer container canvas (draggable placement on worksheet)
            var container = new Canvas
            {
                Width = host.Width,
                Height = host.Height
            };
            container.Children.Add(host);

            // Calculate grid position (row-first layout)
            int plotsPerRow = Math.Max(1, (int)((worksheetWidth - Margin) / (plot.Width + Margin)));
            int col = childIndex % plotsPerRow;
            int row = childIndex / plotsPerRow;

            double x = Margin + col * (plot.Width + Margin);
            double y = Margin + row * (plot.Height + Margin);

            Canvas.SetLeft(container, x);
            Canvas.SetTop(container, y);

            // Drag layer (covers whole plot; reliably receives mouse events)
            var dragLayer = new Border
            {
                Background = Brushes.Transparent,
                Cursor = Cursors.SizeAll,
                Width = host.Width,
                Height = host.Height
            };
            overlay.Children.Add(dragLayer);

            // Keep drag layer sized with host
            host.SizeChanged += (_, __) =>
            {
                dragLayer.Width = host.Width;
                dragLayer.Height = host.Height;
            };

            return new PlotContainer(container, overlay, dragLayer, host, plot, plotBacking, dynamicSurface);
        }
    }
}
