using System;
using System.Linq;
using ScottPlot.Interactivity.UserActionResponses;
using ScottPlot.WPF;
using Worksheet.Models;
using Worksheet.Services;
using Worksheet.Views.PlotViews;
using Worksheet.Views.PlotViews.Axes;
using Worksheet.Views.PlotViews.ContextMenus;
using Worksheet.Views.Support.Gates;

namespace Worksheet.Views.Support
{
    public class PlotFactory
    {
        private static readonly Dictionary<PlotType, (double width, double height)> DefaultPlotSizes = new()
        {
            { PlotType.Histogram, (280, 200) },
            { PlotType.Pseudocolor, (280, 280) },
            { PlotType.SpectralRibbon, (980, 280) },
            { PlotType.Oscilloscope, (560, 280) }
        };

        private readonly AxisFactory _axisFactory;
        private readonly HistogramPlotContextMenu _histogramContextMenu;
        private readonly PseudocolorPlotContextMenu _pseudocolorContextMenu;
        private readonly SpectralRibbonPlotContextMenu _spectralRibbonContextMenu;
        private readonly OscilloscopeContextMenu _oscilloscopeContextMenu;
        private readonly FeatureSelectionStrategy _featureSelectionStrategy;

        public PlotFactory()
            : this(new AxisFactory(), new FeatureSelectionStrategy(), new SpectralRibbonPlotContextMenu())
        {
        }

        public PlotFactory(
            AxisFactory axisFactory,
            FeatureSelectionStrategy featureSelectionStrategy,
            SpectralRibbonPlotContextMenu spectralRibbonContextMenu)
        {
            _axisFactory = axisFactory;
            _featureSelectionStrategy = featureSelectionStrategy;
            _histogramContextMenu = new HistogramPlotContextMenu(_featureSelectionStrategy);
            _pseudocolorContextMenu = new PseudocolorPlotContextMenu(_featureSelectionStrategy);
            _spectralRibbonContextMenu = spectralRibbonContextMenu;
            _oscilloscopeContextMenu = new OscilloscopeContextMenu();
        }

        public WpfPlot CreatePlot(double width, double height)
        {
            var plot = CreateBasePlot(width, height);

            // Add sample data
            plot.Plot.Add.Scatter(
                new double[] { 1, 2, 3, 4, 5 },
                new double[] { 1, 4, 9, 16, 25 });

            return plot;
        }

        public WpfPlot CreatePlot(double width, double height, PlotType plotType, out PlotView plotView)
        {
            var plot = CreateBasePlot(width, height);

            var settings = CreateSettings(plotType);
            plotView = CreatePlotView(plotType, settings);
            try
            {
                plotView.Configure(plot);
            }
            catch (Exception ex)
            {
                // Avoid crashing if a view's Configure() has an issue.
                Worksheet.Services.AppLog.Exception(ex, $"PlotFactory.Configure plotType={plotType} plotId={settings.Id}");
            }

            return plot;
        }

        public WpfPlot CreatePlot(double width, double height, PlotType plotType, AxisScaleType axisScale, out PlotView plotView)
        {
            var plot = CreateBasePlot(width, height);

            var settings = CreateSettings(plotType);
            switch (settings)
            {
                case HistogramSettings histogram:
                    histogram.XAxisScaleType = axisScale;
                    break;
                case PseudocolorSettings pseudocolor:
                    pseudocolor.XAxisScaleType = axisScale;
                    break;
            }
            plotView = CreatePlotView(plotType, settings);
            try
            {
                plotView.Configure(plot);
            }
            catch (Exception ex)
            {
                // Avoid crashing if a view's Configure() has an issue.
                Worksheet.Services.AppLog.Exception(ex, $"PlotFactory.Configure plotType={plotType} plotId={settings.Id} axisScale={axisScale}");
            }

            return plot;
        }

        // New overloads that use default sizes based on plot type
        public WpfPlot CreatePlot(PlotType plotType, out PlotView plotView)
        {
            var (width, height) = GetDefaultSize(plotType);
            return CreatePlot(width, height, plotType, out plotView);
        }

        public WpfPlot CreatePlot(PlotType plotType, AxisScaleType axisScale, out PlotView plotView)
        {
            var (width, height) = GetDefaultSize(plotType);
            return CreatePlot(width, height, plotType, axisScale, out plotView);
        }

        private static (double width, double height) GetDefaultSize(PlotType plotType)
        {
            return DefaultPlotSizes.GetValueOrDefault(plotType, (200, 200));
        }

        public PlotSettings CreateSettings(PlotType plotType)
        {
            return plotType switch
            {
                PlotType.Histogram => new HistogramSettings
                {
                    BinCount = 256,
                    XFeature = 0,
                    XAxisScaleType = AxisScaleType.Logarithmic
                },
                PlotType.Pseudocolor => new PseudocolorSettings
                {
                    BinCount = 256,
                    XFeature = 0,
                    YFeature = 1,
                    XAxisScaleType = AxisScaleType.Logarithmic,
                    YAxisScaleType = AxisScaleType.Logarithmic
                },
                PlotType.SpectralRibbon => new SpectralRibbonSettings
                {
                    BinCount = 256,
                    YAxisScaleType = AxisScaleType.Logarithmic
                },
                PlotType.Oscilloscope => new ScopeSettings(),
                _ => throw new ArgumentOutOfRangeException(nameof(plotType), plotType, "Unsupported plot type.")
            };
        }

        public PlotView CreatePlotView(PlotType plotType, PlotSettings settings)
        {
            return settings switch
            {
                HistogramSettings histogram => new HistogramPlotView(_histogramContextMenu, _axisFactory, histogram, new GateVisualManager()),
                PseudocolorSettings pseudocolor => new PseudocolorPlotView(_pseudocolorContextMenu, pseudocolor, new GateVisualManager()),
                SpectralRibbonSettings spectral => new SpectralRibbonPlotView(_spectralRibbonContextMenu, spectral),
                ScopeSettings scope => new OscilloscopePlotView(_oscilloscopeContextMenu, scope),
                _ => throw new ArgumentOutOfRangeException(nameof(settings), plotType, "Unsupported plot type.")
            };
        }

        private static WpfPlot CreateBasePlot(double width, double height)
        {
            var plot = new WpfPlot
            {
                Width = width,
                Height = height,
            };

            // Disable pan/zoom/etc. by removing common UIP responses
            var uip = plot.UserInputProcessor;
            uip.IsEnabled = true;

            uip.UserActionResponses.RemoveAll(r =>
                r is MouseDragPan ||
                r is MouseDragZoom ||
                r is MouseDragZoomRectangle ||
                r.GetType().Name.Contains("Wheel", StringComparison.OrdinalIgnoreCase) ||
                r.GetType().Name.Contains("Scroll", StringComparison.OrdinalIgnoreCase)
            );

            plot.Plot.FigureBackground.Color = ScottPlot.Color.FromARGB(0);
            plot.Plot.DataBackground.Color = ScottPlot.Color.FromARGB(0);
            plot.Plot.Grid.IsVisible = false;

            // Show the data-area border so thumbs visually "sit" on it
            plot.Plot.DataBorder.Width = 2;
            plot.Plot.Axes.AntiAlias(true);
            plot.Plot.Axes.Hairline(true);
            plot.Plot.Axes.Right.MinimumSize = 20;
            plot.Plot.Axes.Bottom.TickLabelStyle.FontSize = 13;
            plot.Plot.Axes.Left.TickLabelStyle.FontSize = 13;
            plot.Plot.Axes.Bottom.TickLabelStyle.Bold = true;
            plot.Plot.Axes.Left.TickLabelStyle.Bold = true;
            plot.Plot.Axes.Bottom.MajorTickStyle.Length = 6;
            plot.Plot.Axes.Bottom.MajorTickStyle.Width = 2;
            plot.Plot.Axes.Bottom.MinorTickStyle.Length = 4;
            plot.Plot.Axes.Bottom.MinorTickStyle.Width = 1;
            plot.Plot.Axes.Left.MajorTickStyle.Length = 6;
            plot.Plot.Axes.Left.MajorTickStyle.Width = 2;
            plot.Plot.Axes.Left.MinorTickStyle.Length = 4;
            plot.Plot.Axes.Left.MinorTickStyle.Width = 1;
            plot.Plot.Axes.Left.Label.Padding = 50;

            return plot;
        }
    }
}
