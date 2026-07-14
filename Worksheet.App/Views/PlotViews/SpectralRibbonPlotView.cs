using System;
using System.Collections.Generic;
using ScottPlot.WPF;
using Worksheet.Models;
using Worksheet.Models.Data;
using Worksheet.Services;
using Worksheet.Views.PlotViews.Axes;
using Worksheet.Views.PlotViews.ContextMenus;

namespace Worksheet.Views.PlotViews
{
    public class SpectralRibbonPlotView : PlotView<SpectralRibbonSettings>
    {
        private SpectralConfigSnapshot? _lastAppliedConfig;

        public SpectralRibbonPlotView(SpectralRibbonPlotContextMenu contextMenu, SpectralRibbonSettings settings)
            : base(contextMenu, settings)
        {
        }

        public override PlotType PlotType => PlotType.SpectralRibbon;

        public override void Configure(WpfPlot plot)
        {
            int bins = Settings.GetBinCount();
            int channelCount = FeatureSelectionStrategy.ChannelNames.Count;
            if (channelCount <= 0)
                channelCount = 1;

            plot.Plot.DataBackground.Color = ScottPlot.Color.FromARGB(0);
            ApplyAxesAndTicks(plot, bins, channelCount, resetLimits: true);
            _lastAppliedConfig = SpectralConfigSnapshot.Create(Settings, bins, channelCount);
        }

        public override void Render(WpfPlot plot, ProcessedPlotData data)
        {
            if (data is not SpectralRibbonProcessedData spectralData)
                return;

            int bins = spectralData.Bins;
            int channelCount = spectralData.ChannelCount;
            if (ApplyConfigIfChanged(plot, bins, channelCount))
            {
                ExecuteStaticRefresh(plot);
            }

            if (!TryGetBitmapSurface(out var surface))
                return;

            if (spectralData.IsEmpty)
            {
                surface.Clear();
                return;
            }

            surface.PresentBitmap(spectralData.PixelBuffer, spectralData.PixelWidth, spectralData.PixelHeight);
        }

        public override void Clear(WpfPlot plot)
        {
            ClearDynamicSurface();
        }

        public override void InvalidateStatic(WpfPlot plot)
        {
            int bins = Settings.GetBinCount();
            int channelCount = FeatureSelectionStrategy.ChannelNames.Count;
            if (channelCount <= 0)
                channelCount = 1;

            ApplyConfigIfChanged(plot, bins, channelCount);
            ExecuteStaticRefresh(plot);
        }

        private bool ApplyConfigIfChanged(WpfPlot plot, int bins, int channelCount)
        {
            var current = SpectralConfigSnapshot.Create(Settings, bins, channelCount);
            if (_lastAppliedConfig.HasValue && _lastAppliedConfig.Value.Equals(current))
                return false;

            ApplyAxesAndTicks(plot, bins, channelCount, resetLimits: false);
            _lastAppliedConfig = current;
            return true;
        }

        private void ApplyAxesAndTicks(WpfPlot plot, int bins, int channelCount, bool resetLimits)
        {
            ApplyChannelTicks(plot, channelCount, resetLimits);
            ApplyYAxisTicks(plot, bins, resetLimits);
        }

        private static void ApplyChannelTicks(WpfPlot plot, int channelCount, bool resetLimits)
        {
            var channelNames = FeatureSelectionStrategy.ChannelNames;
            var positions = new double[channelCount];
            var labels = new string[channelCount];

            for (int i = 0; i < channelCount; i++)
            {
                positions[i] = i + 0.5;

                // Get channel name and remove "nm" suffix if present
                var channelName = i < channelNames.Count ? channelNames[i] : $"Channel {i + 1}";
                labels[i] = channelName.EndsWith("nm", StringComparison.OrdinalIgnoreCase)
                    ? channelName.Substring(0, channelName.Length - 2)
                    : channelName;
            }

            // Add minor tick positions at channel edges (integer boundaries)
            var minorPositions = new double[channelCount + 1];
            for (int i = 0; i <= channelCount; i++)
                minorPositions[i] = i;

            var tickGen = new FixedTickGenerator(
                positions,
                labels,
                minorPositions);
            tickGen.MaxTickCount = channelCount;

            plot.Plot.Axes.Bottom.TickGenerator = tickGen;
            if (resetLimits)
                plot.Plot.Axes.SetLimitsX(0, channelCount);

            // Configure rotated tick labels - ADJUST THESE VALUES TO ITERATE:
            plot.Plot.Axes.Bottom.TickLabelStyle.Rotation = 90;  // Try: 90, -90, 45, -45
            plot.Plot.Axes.Bottom.TickLabelStyle.Alignment = ScottPlot.Alignment.UpperLeft;  // Try: UpperLeft, UpperCenter, UpperRight, MiddleLeft, MiddleCenter, MiddleRight, LowerLeft, LowerCenter, LowerRight
            plot.Plot.Axes.Bottom.TickLabelStyle.OffsetX = 6;  // Try: 0, 5, 10, -5, -10
            plot.Plot.Axes.Bottom.TickLabelStyle.OffsetY = 3;  // Try: 0, 5, 10, -5, -10
            plot.Plot.Axes.Bottom.MinimumSize = 50;

            plot.Plot.Axes.Bottom.MinorTickStyle.Length = 0;
            plot.Plot.Axes.Bottom.MinorTickStyle.Width = 0;
        }

        private void ApplyYAxisTicks(WpfPlot plot, int bins, bool resetLimits)
        {
            switch (Settings.YAxisScaleType)
            {
                case ScaleType.Linear:
                    plot.Plot.Axes.Left.TickGenerator = LinearAxisItem.CreateDataTickGenerator(Settings);
                    if (resetLimits)
                        plot.Plot.Axes.SetLimitsY(0, bins);
                    break;
                case ScaleType.Logarithmic:
                    plot.Plot.Axes.Left.TickGenerator = LogarithmicAxisItem.CreateDataTickGenerator(Settings);
                    if (resetLimits)
                        plot.Plot.Axes.SetLimitsY(0, bins);
                    break;
                default:
                    break;
            }
        }

        private readonly record struct SpectralConfigSnapshot(
            int Bins,
            int ChannelCount,
            ScaleType YAxisScaleType,
            double MinValue,
            double MaxValue)
        {
            public static SpectralConfigSnapshot Create(SpectralRibbonSettings settings, int bins, int channelCount) =>
                new(
                    bins,
                    channelCount,
                    settings.YAxisScaleType,
                    settings.MinValue,
                    settings.MaxValue);
        }
    }
}
