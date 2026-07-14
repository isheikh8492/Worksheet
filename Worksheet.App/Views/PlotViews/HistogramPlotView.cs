using ScottPlot.WPF;
using Worksheet.Models;
using Worksheet.Models.Data;
using Worksheet.Models.Gates;
using Worksheet.Services;
using Worksheet.Views.PlotViews.Axes;
using Worksheet.Views.PlotViews.ContextMenus;
using Worksheet.Views.Support.Gates;

namespace Worksheet.Views.PlotViews
{
    public class HistogramPlotView : PlotView<HistogramSettings>
    {
        private readonly AxisFactory _axisFactory;
        private readonly GateVisualManager _gateVisualManager;
        private readonly HistogramYAxisItem _yAxisItem = new();
        private HistogramConfigSnapshot? _lastAppliedConfig;
        private byte[] _pixelBuffer = Array.Empty<byte>();
        private int _pixelWidth;
        private int _pixelHeight;
        private double _yAxisUpperBound = 1000;

        public Action<GateSettings>? GateSettingsSink { get; set; }
        public Action<Guid>? GateRemovedSink { get; set; }

        public HistogramPlotView(
            HistogramPlotContextMenu contextMenu,
            AxisFactory axisFactory,
            HistogramSettings settings,
            GateVisualManager gateVisualManager)
            : base(contextMenu, settings)
        {
            _axisFactory = axisFactory;
            _gateVisualManager = gateVisualManager;
        }

        public override PlotType PlotType => PlotType.Histogram;

        public ScaleType CurrentAxisScale => Settings.XAxisScaleType;

        public override void Configure(WpfPlot plot)
        {
            plot.Plot.DataBackground.Color = ScottPlot.Color.FromARGB(0);
            plot.Plot.Axes.Margins(bottom: 0);
            plot.Plot.Axes.SetLimitsY(0, 1);
            plot.Plot.YLabel("Frequency");
            plot.Plot.XLabel(GetXAxisLabel(Settings.XFeature));
            _axisFactory.Apply(Settings.XAxisScaleType, plot, Settings);
            _yAxisUpperBound = 1000;
            _yAxisItem.Apply(plot, _yAxisUpperBound);
            _lastAppliedConfig = HistogramConfigSnapshot.From(Settings);
        }

        public override void Render(WpfPlot plot, ProcessedPlotData data)
        {
            if (data is not HistogramProcessedData histogram)
                return;

            if (histogram.ScaleType != Settings.XAxisScaleType)
                return;

            if (histogram.BinCount != Settings.GetBinCount())
                return;

            bool staticChanged = ApplyConfigIfChanged(plot);
            bool yTickLabelsChanged = UpdateHistogramScale(histogram.Counts);
            if (staticChanged || yTickLabelsChanged)
            {
                ExecuteStaticRefresh(plot, () => _yAxisItem.Apply(plot, _yAxisUpperBound));
            }

            RenderHistogramDynamic(histogram);
        }

        public override void Clear(WpfPlot plot)
        {
            ClearDynamicSurface();
            _yAxisUpperBound = 1000;
            ExecuteStaticRefresh(plot, () =>
            {
                plot.Plot.Axes.SetLimitsY(0, 1);
                _yAxisItem.Apply(plot, _yAxisUpperBound);
            });
        }

        public override void InvalidateStatic(WpfPlot plot)
        {
            ApplyConfigIfChanged(plot);
            ExecuteStaticRefresh(plot);
        }

        public void UpdateAxisScale(PlotItem plotItem, ScaleType newScale)
        {
            Settings.XAxisScaleType = newScale;
        }

        internal void AttachGateInteractions(PlotItem plotItem)
        {
            _gateVisualManager.Attach(
                plotItem,
                () => Settings.GetBinCount(),
                () => Settings.Id,
                () => Settings.PlotType,
                GateSettingsSink,
                GateRemovedSink);
        }

        internal void BeginAddLineGate(PlotItem plotItem)
        {
            _gateVisualManager.BeginAddLineGate(plotItem);
        }

        internal bool HasSelectedGate() => _gateVisualManager.HasSelectedGate;

        internal bool RemoveSelectedGate(PlotItem plotItem) => _gateVisualManager.RemoveSelectedGate(plotItem);

        private bool ApplyConfigIfChanged(WpfPlot plot)
        {
            var current = HistogramConfigSnapshot.From(Settings);
            if (_lastAppliedConfig.HasValue && _lastAppliedConfig.Value.Equals(current))
                return false;

            plot.Plot.XLabel(GetXAxisLabel(Settings.XFeature));
            _axisFactory.Apply(Settings.XAxisScaleType, plot, Settings);
            _yAxisItem.Apply(plot, _yAxisUpperBound);
            _lastAppliedConfig = current;
            return true;
        }

        private bool UpdateHistogramScale(double[] counts)
        {
            double maxCount = 0;
            for (int i = 0; i < counts.Length; i++)
            {
                if (counts[i] > maxCount)
                    maxCount = counts[i];
            }

            double snappedUpperBound = _yAxisItem.GetSnappedUpperBound(maxCount);
            if (snappedUpperBound.Equals(_yAxisUpperBound))
                return false;

            _yAxisUpperBound = snappedUpperBound;
            return true;
        }

        private void RenderHistogramDynamic(HistogramProcessedData histogram)
        {
            if (!TryGetBitmapSurface(out var surface))
                return;

            int width = surface.TargetWidth;
            int height = surface.TargetHeight;

            if (width <= 0 || height <= 0)
            {
                surface.Clear();
                return;
            }

            if (_pixelBuffer.Length != width * height * 4)
            {
                _pixelBuffer = new byte[width * height * 4];
                _pixelWidth = width;
                _pixelHeight = height;
            }
            else
            {
                Array.Clear(_pixelBuffer, 0, _pixelBuffer.Length);
            }

            double maxCount = _yAxisUpperBound <= 0 ? 1 : _yAxisUpperBound;
            bool hasAnyData = false;
            int binCount = histogram.Counts.Length;

            for (int i = 0; i < binCount; i++)
            {
                double value = histogram.Counts[i];
                if (value <= 0)
                    continue;

                hasAnyData = true;
                int x0 = (int)Math.Floor((double)i / binCount * width);
                int x1 = (int)Math.Ceiling((double)(i + 1) / binCount * width);
                x0 = Math.Clamp(x0, 0, width - 1);
                x1 = Math.Clamp(Math.Max(x0 + 1, x1), 1, width);

                double heightFraction = Math.Clamp(value / maxCount, 0, 1);
                int filledHeight = Math.Clamp((int)Math.Round(heightFraction * height), 0, height);
                int yStart = Math.Max(0, height - filledHeight);

                for (int y = yStart; y < height; y++)
                {
                    int rowOffset = y * width * 4;
                    for (int x = x0; x < x1; x++)
                    {
                        int pixelIndex = rowOffset + (x * 4);
                        _pixelBuffer[pixelIndex + 0] = 80;
                        _pixelBuffer[pixelIndex + 1] = 175;
                        _pixelBuffer[pixelIndex + 2] = 76;
                        _pixelBuffer[pixelIndex + 3] = 255;
                    }
                }
            }

            if (!hasAnyData)
            {
                surface.Clear();
                return;
            }

            surface.PresentBitmap(_pixelBuffer, _pixelWidth, _pixelHeight);
        }

        private readonly record struct HistogramConfigSnapshot(
            int BinCount,
            int XFeature,
            ScaleType XAxisScaleType,
            double MinValue,
            double MaxValue)
        {
            public static HistogramConfigSnapshot From(HistogramSettings settings) =>
                new(
                    settings.GetBinCount(),
                    settings.XFeature,
                    settings.XAxisScaleType,
                    settings.MinValue,
                    settings.MaxValue);
        }

        private static string GetXAxisLabel(int featureIndex)
        {
            if (FeatureSelectionStrategy.TryGetChannelWavelength(featureIndex, out var wavelength))
                return $"Intensity ({wavelength})";

            return $"Intensity (Channel {featureIndex + 1})";
        }
    }
}
