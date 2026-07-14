using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using ScottPlot.WPF;
using Worksheet.App.Views.PlotViews;

using Worksheet.App.Models;
namespace Worksheet.App.Models
{
    /// <summary>
    /// A worksheet item containing a ScottPlot chart.
    /// </summary>
    public class PlotItem : IWorksheetItem
    {
        public Canvas Container { get; }
        public double Width => Container.Width;
        public double Height => Container.Height;

        public WpfPlot Plot { get; }
        public PlotContainer PlotContainer { get; }

        public PlotView? PlotView { get; set; }
        public Action<PlotItem>? OnCloseRequested { get; set; }

        private readonly Thumb[] _thumbs;

        public PlotItem(WpfPlot plot, PlotContainer container, Thumb[] thumbs)
        {
            Plot = plot;
            PlotContainer = container;
            Container = container.Container;
            _thumbs = thumbs;
        }

        public void OnSelect()
        {
            foreach (var thumb in _thumbs)
                thumb.Visibility = Visibility.Visible;
        }

        public void OnDeselect()
        {
            foreach (var thumb in _thumbs)
                thumb.Visibility = Visibility.Collapsed;
        }
    }
}
