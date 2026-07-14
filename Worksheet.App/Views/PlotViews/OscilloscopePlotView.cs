using System;
using System.Collections.Generic;
using ScottPlot;
using ScottPlot.Plottables;
using ScottPlot.WPF;
using Worksheet.Core.Models;
using Worksheet.Core.Models.Data;
using Worksheet.App.Views.ContextMenus;

namespace Worksheet.App.Views.PlotViews
{
    public class OscilloscopePlotView : PlotView<ScopeSettings>
    {
        private const double FixedYMin = -0.18;
        private const double FixedYMax = 0.52;
        private readonly List<Signal> _signals = new();
        private readonly Color[] _channelColors =
        [
            Color.FromHex("#2196F3"),
            Color.FromHex("#4CAF50"),
            Color.FromHex("#FF9800"),
            Color.FromHex("#E91E63"),
        ];

        public OscilloscopePlotView(
            OscilloscopeContextMenu contextMenu,
            ScopeSettings settings)
            : base(contextMenu, settings)
        {
        }

        public override PlotType PlotType => PlotType.Oscilloscope;

        public override void Configure(WpfPlot plot)
        {
            ConfigureAxes(plot);
            SetSignalLimits(plot, Settings.InitialSampleCount);
            plot.Refresh();
        }

        public override void Render(WpfPlot plot, ProcessedPlotData data)
        {
            if (data is not OscilloscopeProcessedData oscilloscopeData)
                return;

            if (oscilloscopeData.IsEmpty || oscilloscopeData.Signals.Length == 0)
            {
                RenderOnce(plot, () =>
                {
                    plot.Plot.Clear();
                    _signals.Clear();
                    ConfigureAxes(plot);
                    SetSignalLimits(plot, 1);
                });
                return;
            }

            RenderOnce(plot, () =>
            {
                plot.Plot.Clear();
                _signals.Clear();
                ConfigureAxes(plot);

                for (int channel = 0; channel < oscilloscopeData.Signals.Length; channel++)
                {
                    var signal = plot.Plot.Add.Signal(oscilloscopeData.Signals[channel]);
                    signal.Color = _channelColors[channel % _channelColors.Length];
                    signal.LineWidth = 1;
                    _signals.Add(signal);
                }

                SetSignalLimits(plot, oscilloscopeData.TimestampCount);
            });
        }

        private static void ConfigureAxes(WpfPlot plot)
        {
            plot.Plot.YLabel("Voltage (V)");
            plot.Plot.XLabel("Time (samples)");
        }

        private static void SetSignalLimits(WpfPlot plot, int timestampCount)
        {
            plot.Plot.Axes.SetLimitsX(0, Math.Max(1, timestampCount - 1));
            plot.Plot.Axes.SetLimitsY(FixedYMin, FixedYMax);
        }
    }
}
