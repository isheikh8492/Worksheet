using System;
using System.Collections.Generic;
using System.Linq;
using Worksheet.Core.Models;
using Worksheet.Core.Models.Data;

using Worksheet.Core.Services;
using Worksheet.Core.Buffers;
namespace Worksheet.Processing
{
    public class PlotProcessor
    {
        private static readonly HeatmapColorPalette HeatmapPalette = HeatmapColorPalette.MellowRainbow;
        private readonly IChannelDataBuffer _buffer;
        private readonly object _stateLock = new();
        private readonly Dictionary<Guid, HistogramProcessingState> _histogramStates = new();
        private readonly Dictionary<Guid, PseudocolorProcessingState> _pseudocolorStates = new();
        private readonly Dictionary<Guid, SpectralRibbonProcessingState> _spectralStates = new();
        private readonly object _statsLock = new();
        private long _deltaAppliedCount;
        private long _fullRebuildCount;
        private long _sequenceGapCount;

        public PlotProcessor(IChannelDataBuffer buffer)
        {
            _buffer = buffer;
        }

        public ProcessedPlotData? Process(PlotSettings settings)
        {
            return Process(settings, RenderTargetSize.Empty);
        }

        public ProcessedPlotData? Process(PlotSettings settings, RenderTargetSize targetSize)
        {
            try
            {
                return settings switch
                {
                    HistogramSettings histogram => ProcessHistogram(histogram),
                    PseudocolorSettings pseudocolor => ProcessHeatmap(pseudocolor, targetSize),
                    SpectralRibbonSettings spectral => ProcessSpectralRibbon(spectral, targetSize),
                    _ => throw new ArgumentOutOfRangeException(nameof(settings), settings.PlotType, "Unsupported plot type.")
                };
            }
            catch (Exception ex)
            {
                AppLog.Exception(ex, $"PlotProcessor.Process plotType={settings.PlotType} plotId={settings.Id}");
                return null;
            }
        }

        public (long deltaAppliedCount, long fullRebuildCount, long sequenceGapCount) GetDeltaStats()
        {
            lock (_statsLock)
            {
                return (_deltaAppliedCount, _fullRebuildCount, _sequenceGapCount);
            }
        }

        public void ResetIncrementalState()
        {
            lock (_stateLock)
            {
                _histogramStates.Clear();
                _pseudocolorStates.Clear();
                _spectralStates.Clear();
            }

            lock (_statsLock)
            {
                _deltaAppliedCount = 0;
                _fullRebuildCount = 0;
                _sequenceGapCount = 0;
            }
        }

        private ProcessedPlotData ProcessHistogram(HistogramSettings settings)
        {
            ChannelWindowSnapshot snapshot = _buffer.GetSnapshot(settings.XFeature);
            int bins = settings.GetBinCount();
            Scale scale = Scale.Create(settings.XAxisScaleType, settings.MinValue, settings.MaxValue, bins);

            HistogramProcessingState state;
            lock (_stateLock)
            {
                if (!_histogramStates.TryGetValue(settings.Id, out state!))
                {
                    state = new HistogramProcessingState(settings.Id, bins, settings.XFeature, settings.XAxisScaleType, settings.MinValue, settings.MaxValue, snapshot.Capacity);
                    _histogramStates[settings.Id] = state;
                }
                else if (!state.Matches(bins, settings.XFeature, settings.XAxisScaleType, settings.MinValue, settings.MaxValue, snapshot.Capacity))
                {
                    state.Reset(bins, settings.XFeature, settings.XAxisScaleType, settings.MinValue, settings.MaxValue, snapshot.Capacity);
                    RecordFullRebuild();
                }
            }

            if (snapshot.Count <= 0)
            {
                state.ClearData(snapshot.EndSequence);
                return BuildHistogramData(settings.Id, state.Counts, bins, settings.XAxisScaleType);
            }

            if (NeedsRebuild(state.LastProcessedSequence, snapshot))
            {
                if (state.LastProcessedSequence < snapshot.StartSequence && state.LastProcessedSequence != 0)
                    RecordSequenceGap();

                state.ClearData(snapshot.StartSequence);
                ApplyHistogramRange(state, snapshot, snapshot.StartSequence, snapshot.EndSequence, scale);
                state.LastProcessedSequence = snapshot.EndSequence;
                RecordFullRebuild();
            }
            else
            {
                long fromSequence = state.LastProcessedSequence;
                if (fromSequence < snapshot.EndSequence)
                {
                    ApplyHistogramRange(state, snapshot, fromSequence, snapshot.EndSequence, scale);
                    state.LastProcessedSequence = snapshot.EndSequence;
                    RecordDeltaApplied(snapshot.EndSequence - fromSequence);
                }

                TrimHistogramToWindow(state, snapshot.Count);
            }

            return BuildHistogramData(settings.Id, state.Counts, bins, settings.XAxisScaleType);
        }

        private static HistogramProcessedData BuildHistogramData(Guid plotId, double[] counts, int bins, ScaleType scaleType)
        {
            var outCounts = new double[bins];
            Array.Copy(counts, outCounts, bins);

            var positions = new double[bins];
            for (int i = 0; i < bins; i++)
                positions[i] = i + 0.5;

            return new HistogramProcessedData(plotId, positions, outCounts, bins, scaleType);
        }

        private static void ApplyHistogramRange(
            HistogramProcessingState state,
            ChannelWindowSnapshot snapshot,
            long fromSequence,
            long toSequence,
            Scale scale)
        {
            for (long sequence = fromSequence; sequence < toSequence; sequence++)
            {
                int physicalIndex = snapshot.PhysicalIndexForSequence(sequence);
                double value = snapshot.Values[physicalIndex];
                int bin = scale.ToBin(value);
                state.Counts[bin]++;
                AppendHistogramContribution(state, bin);
            }
        }

        private static void AppendHistogramContribution(HistogramProcessingState state, int bin)
        {
            if (state.RingCount == state.RingBins.Length)
            {
                int evicted = state.RingBins[state.RingStart];
                state.Counts[evicted]--;
                state.RingStart = (state.RingStart + 1) % state.RingBins.Length;
                state.RingCount--;
            }

            int writeIndex = (state.RingStart + state.RingCount) % state.RingBins.Length;
            state.RingBins[writeIndex] = bin;
            state.RingCount++;
        }

        private static void TrimHistogramToWindow(HistogramProcessingState state, int windowCount)
        {
            while (state.RingCount > windowCount)
            {
                int evicted = state.RingBins[state.RingStart];
                state.Counts[evicted]--;
                state.RingStart = (state.RingStart + 1) % state.RingBins.Length;
                state.RingCount--;
            }
        }

        private ProcessedPlotData ProcessHeatmap(PseudocolorSettings settings, RenderTargetSize targetSize)
        {
            MultiChannelWindowSnapshot snapshot = _buffer.GetSnapshot(settings.XFeature, settings.YFeature);
            int bins = settings.GetBinCount();
            int pixelWidth = targetSize.HasPixels ? targetSize.PixelWidth : bins;
            int pixelHeight = targetSize.HasPixels ? targetSize.PixelHeight : bins;
            bool isEmpty = snapshot.Count <= 0;
            Scale xScale = Scale.Create(settings.XAxisScaleType, settings.MinValue, settings.MaxValue, bins);
            Scale yScale = Scale.Create(settings.YAxisScaleType, settings.MinValue, settings.MaxValue, bins);

            PseudocolorProcessingState state;
            lock (_stateLock)
            {
                if (!_pseudocolorStates.TryGetValue(settings.Id, out state!))
                {
                    state = new PseudocolorProcessingState(settings.Id, bins, settings.XFeature, settings.YFeature, settings.XAxisScaleType, settings.YAxisScaleType, settings.MinValue, settings.MaxValue, snapshot.Capacity);
                    _pseudocolorStates[settings.Id] = state;
                }
                else if (!state.Matches(bins, settings.XFeature, settings.YFeature, settings.XAxisScaleType, settings.YAxisScaleType, settings.MinValue, settings.MaxValue, snapshot.Capacity))
                {
                    state.Reset(bins, settings.XFeature, settings.YFeature, settings.XAxisScaleType, settings.YAxisScaleType, settings.MinValue, settings.MaxValue, snapshot.Capacity);
                    RecordFullRebuild();
                }
            }

            if (snapshot.Count <= 0)
            {
                state.ClearData(snapshot.EndSequence);
                RenderPseudocolorPixels(state, bins, pixelWidth, pixelHeight);
                return new HeatmapProcessedData(settings.Id, state.Normalized, state.PixelBuffer, bins, pixelWidth, pixelHeight, isEmpty: true);
            }

            if (NeedsRebuild(state.LastProcessedSequence, snapshot))
            {
                if (state.LastProcessedSequence < snapshot.StartSequence && state.LastProcessedSequence != 0)
                    RecordSequenceGap();

                state.ClearData(snapshot.StartSequence);
                ApplyPseudocolorRange(state, snapshot, snapshot.StartSequence, snapshot.EndSequence, xScale, yScale, bins);
                state.LastProcessedSequence = snapshot.EndSequence;
                RecordFullRebuild();
            }
            else
            {
                long fromSequence = state.LastProcessedSequence;
                if (fromSequence < snapshot.EndSequence)
                {
                    ApplyPseudocolorRange(state, snapshot, fromSequence, snapshot.EndSequence, xScale, yScale, bins);
                    state.LastProcessedSequence = snapshot.EndSequence;
                    RecordDeltaApplied(snapshot.EndSequence - fromSequence);
                }

                TrimPseudocolorToWindow(state, snapshot.Count);
            }

            NormalizePseudocolor(state, bins, pixelWidth, pixelHeight);
            return new HeatmapProcessedData(settings.Id, state.Normalized, state.PixelBuffer, bins, pixelWidth, pixelHeight, isEmpty: isEmpty);
        }

        private static void ApplyPseudocolorRange(
            PseudocolorProcessingState state,
            MultiChannelWindowSnapshot snapshot,
            long fromSequence,
            long toSequence,
            Scale xScale,
            Scale yScale,
            int bins)
        {
            for (long sequence = fromSequence; sequence < toSequence; sequence++)
            {
                int xPhysicalIndex = snapshot.PhysicalIndexForSequence(sequence);
                int yPhysicalIndex = xPhysicalIndex;
                double xv = snapshot.ChannelValues[0][xPhysicalIndex];
                double yv = snapshot.ChannelValues[1][yPhysicalIndex];
                int xBin = xScale.ToBin(xv);
                int yBin = yScale.ToBin(yv);
                int row = (bins - 1) - yBin;
                state.RawCounts[row, xBin]++;
                AppendPseudocolorContribution(state, row, xBin);
            }
        }

        private static void AppendPseudocolorContribution(PseudocolorProcessingState state, int row, int xBin)
        {
            if (state.RingCount == state.RingPackedBins.Length)
            {
                int packedEvicted = state.RingPackedBins[state.RingStart];
                int evictedX = packedEvicted & 0xFFFF;
                int evictedRow = (packedEvicted >> 16) & 0xFFFF;
                state.RawCounts[evictedRow, evictedX]--;
                state.RingStart = (state.RingStart + 1) % state.RingPackedBins.Length;
                state.RingCount--;
            }

            int writeIndex = (state.RingStart + state.RingCount) % state.RingPackedBins.Length;
            state.RingPackedBins[writeIndex] = (row << 16) | xBin;
            state.RingCount++;
        }

        private static void TrimPseudocolorToWindow(PseudocolorProcessingState state, int windowCount)
        {
            while (state.RingCount > windowCount)
            {
                int packed = state.RingPackedBins[state.RingStart];
                int x = packed & 0xFFFF;
                int row = (packed >> 16) & 0xFFFF;
                state.RawCounts[row, x]--;
                state.RingStart = (state.RingStart + 1) % state.RingPackedBins.Length;
                state.RingCount--;
            }
        }

        private static void NormalizePseudocolor(PseudocolorProcessingState state, int bins, int pixelWidth, int pixelHeight)
        {
            int max = 0;
            for (int y = 0; y < bins; y++)
                for (int x = 0; x < bins; x++)
                    if (state.RawCounts[y, x] > max) max = state.RawCounts[y, x];

            if (max <= 0)
            {
                for (int y = 0; y < bins; y++)
                    for (int x = 0; x < bins; x++)
                        state.Normalized[y, x] = 0;

                RenderPseudocolorPixels(state, bins, pixelWidth, pixelHeight);
                return;
            }

            for (int y = 0; y < bins; y++)
                for (int x = 0; x < bins; x++)
                {
                    int raw = state.RawCounts[y, x];
                    if (raw == 0)
                    {
                        state.Normalized[y, x] = double.NaN;
                        continue;
                    }

                    double normalized = (double)raw / max;
                    state.Normalized[y, x] = normalized;
                }

            RenderPseudocolorPixels(state, bins, pixelWidth, pixelHeight);
        }

        private static void RenderPseudocolorPixels(PseudocolorProcessingState state, int bins, int pixelWidth, int pixelHeight)
        {
            state.EnsurePixelBuffer(pixelWidth, pixelHeight);

            for (int py = 0; py < pixelHeight; py++)
            {
                int binY = Math.Clamp(py * bins / pixelHeight, 0, bins - 1);
                int rowOffset = py * pixelWidth * 4;
                for (int px = 0; px < pixelWidth; px++)
                {
                    int binX = Math.Clamp(px * bins / pixelWidth, 0, bins - 1);
                    int pixelIndex = rowOffset + (px * 4);
                    HeatmapPalette.WriteNormalizedPixel(state.Normalized[binY, binX], state.PixelBuffer, pixelIndex);
                }
            }
        }

        private ProcessedPlotData ProcessSpectralRibbon(SpectralRibbonSettings settings, RenderTargetSize targetSize)
        {
            var channelIndices = FeatureSelectionStrategy.FilteredChannelIndices;
            int channelCount = channelIndices.Count;
            int bins = settings.GetBinCount();
            int fallbackWidth = Math.Max(1, channelCount);
            int pixelWidth = targetSize.HasPixels ? targetSize.PixelWidth : fallbackWidth;
            int pixelHeight = targetSize.HasPixels ? targetSize.PixelHeight : bins;

            if (channelCount == 0)
            {
                var emptyData = new double[bins, 1];
                var emptyPixels = new byte[pixelWidth * pixelHeight * 4];
                return new SpectralRibbonProcessedData(settings.Id, emptyData, emptyPixels, bins, 1, pixelWidth, pixelHeight, Array.Empty<string>(), isEmpty: true);
            }

            MultiChannelWindowSnapshot snapshot = _buffer.GetSnapshot(channelIndices.ToArray());
            Scale scale = Scale.Create(settings.YAxisScaleType, settings.MinValue, settings.MaxValue, bins);

            int channelHash = ComputeChannelHash(channelIndices);
            SpectralRibbonProcessingState state;
            lock (_stateLock)
            {
                if (!_spectralStates.TryGetValue(settings.Id, out state!))
                {
                    state = new SpectralRibbonProcessingState(settings.Id, bins, channelIndices.ToArray(), channelHash, settings.YAxisScaleType, settings.MinValue, settings.MaxValue, snapshot.Capacity);
                    _spectralStates[settings.Id] = state;
                }
                else if (!state.Matches(bins, channelHash, settings.YAxisScaleType, settings.MinValue, settings.MaxValue, snapshot.Capacity))
                {
                    state.Reset(bins, channelIndices.ToArray(), channelHash, settings.YAxisScaleType, settings.MinValue, settings.MaxValue, snapshot.Capacity);
                    RecordFullRebuild();
                }
            }

            if (snapshot.Count <= 0)
            {
                state.ClearData(snapshot.EndSequence);
                RenderSpectralPixels(state, bins, pixelWidth, pixelHeight);
                return new SpectralRibbonProcessedData(settings.Id, state.Normalized, state.PixelBuffer, bins, state.ChannelCount, pixelWidth, pixelHeight, Array.Empty<string>(), isEmpty: true);
            }

            if (NeedsRebuild(state.LastProcessedSequence, snapshot))
            {
                if (state.LastProcessedSequence < snapshot.StartSequence && state.LastProcessedSequence != 0)
                    RecordSequenceGap();

                state.ClearData(snapshot.StartSequence);
                ApplySpectralRange(state, snapshot, snapshot.StartSequence, snapshot.EndSequence, scale, bins);
                state.LastProcessedSequence = snapshot.EndSequence;
                RecordFullRebuild();
            }
            else
            {
                long fromSequence = state.LastProcessedSequence;
                if (fromSequence < snapshot.EndSequence)
                {
                    ApplySpectralRange(state, snapshot, fromSequence, snapshot.EndSequence, scale, bins);
                    state.LastProcessedSequence = snapshot.EndSequence;
                    RecordDeltaApplied(snapshot.EndSequence - fromSequence);
                }

                TrimSpectralToWindow(state, snapshot.Count);
            }

            NormalizeSpectral(state, bins, pixelWidth, pixelHeight);
            return new SpectralRibbonProcessedData(settings.Id, state.Normalized, state.PixelBuffer, bins, state.ChannelCount, pixelWidth, pixelHeight, Array.Empty<string>(), isEmpty: snapshot.Count <= 0);
        }

        private static void ApplySpectralRange(
            SpectralRibbonProcessingState state,
            MultiChannelWindowSnapshot snapshot,
            long fromSequence,
            long toSequence,
            Scale scale,
            int bins)
        {
            int channelCount = state.ChannelCount;
            for (long sequence = fromSequence; sequence < toSequence; sequence++)
            {
                if (state.RingCount == state.RingRows.GetLength(0))
                    EvictOldestSpectralContribution(state);

                int writeIndex = (state.RingStart + state.RingCount) % state.RingRows.GetLength(0);
                int physicalIndex = snapshot.PhysicalIndexForSequence(sequence);
                for (int c = 0; c < channelCount; c++)
                {
                    double value = snapshot.ChannelValues[c][physicalIndex];
                    int bin = scale.ToBin(value);
                    int row = (bins - 1) - bin;
                    state.RawCounts[row, c]++;
                    state.RingRows[writeIndex, c] = (ushort)row;
                }

                state.RingCount++;
            }
        }

        private static void EvictOldestSpectralContribution(SpectralRibbonProcessingState state)
        {
            int channelCount = state.ChannelCount;
            int evictIndex = state.RingStart;
            for (int c = 0; c < channelCount; c++)
            {
                int row = state.RingRows[evictIndex, c];
                state.RawCounts[row, c]--;
            }

            state.RingStart = (state.RingStart + 1) % state.RingRows.GetLength(0);
            state.RingCount--;
        }

        private static void TrimSpectralToWindow(SpectralRibbonProcessingState state, int windowCount)
        {
            while (state.RingCount > windowCount)
                EvictOldestSpectralContribution(state);
        }

        private static void NormalizeSpectral(SpectralRibbonProcessingState state, int bins, int pixelWidth, int pixelHeight)
        {
            int max = 0;
            for (int y = 0; y < bins; y++)
                for (int c = 0; c < state.ChannelCount; c++)
                    if (state.RawCounts[y, c] > max) max = state.RawCounts[y, c];

            if (max <= 0)
            {
                for (int y = 0; y < bins; y++)
                    for (int c = 0; c < state.ChannelCount; c++)
                        state.Normalized[y, c] = 0;

                RenderSpectralPixels(state, bins, pixelWidth, pixelHeight);
                return;
            }

            for (int y = 0; y < bins; y++)
                for (int c = 0; c < state.ChannelCount; c++)
                {
                    int raw = state.RawCounts[y, c];
                    if (raw == 0)
                    {
                        state.Normalized[y, c] = double.NaN;
                        continue;
                    }

                    double normalized = (double)raw / max;
                    state.Normalized[y, c] = normalized;
                }

            RenderSpectralPixels(state, bins, pixelWidth, pixelHeight);
        }

        private static void RenderSpectralPixels(SpectralRibbonProcessingState state, int bins, int pixelWidth, int pixelHeight)
        {
            state.EnsurePixelBuffer(pixelWidth, pixelHeight);

            for (int py = 0; py < pixelHeight; py++)
            {
                int binY = Math.Clamp(py * bins / pixelHeight, 0, bins - 1);
                int rowOffset = py * pixelWidth * 4;
                for (int px = 0; px < pixelWidth; px++)
                {
                    int channel = Math.Clamp(px * state.ChannelCount / pixelWidth, 0, state.ChannelCount - 1);
                    int pixelIndex = rowOffset + (px * 4);
                    HeatmapPalette.WriteNormalizedPixel(state.Normalized[binY, channel], state.PixelBuffer, pixelIndex);
                }
            }
        }

        private static bool NeedsRebuild(long lastProcessedSequence, ChannelWindowSnapshot snapshot)
        {
            if (lastProcessedSequence == 0)
                return true;
            if (lastProcessedSequence < snapshot.StartSequence)
                return true;
            if (lastProcessedSequence > snapshot.EndSequence)
                return true;
            return false;
        }

        private static bool NeedsRebuild(long lastProcessedSequence, MultiChannelWindowSnapshot snapshot)
        {
            if (lastProcessedSequence == 0)
                return true;
            if (lastProcessedSequence < snapshot.StartSequence)
                return true;
            if (lastProcessedSequence > snapshot.EndSequence)
                return true;
            return false;
        }

        private static int ComputeChannelHash(IReadOnlyList<int> channels)
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < channels.Count; i++)
                    hash = hash * 31 + channels[i];
                return hash;
            }
        }

        private void RecordDeltaApplied(long deltaCount)
        {
            lock (_statsLock)
            {
                _deltaAppliedCount += Math.Max(0, deltaCount);
            }
        }

        private void RecordFullRebuild()
        {
            lock (_statsLock)
            {
                _fullRebuildCount++;
            }
        }

        private void RecordSequenceGap()
        {
            lock (_statsLock)
            {
                _sequenceGapCount++;
            }
        }

        private sealed class HistogramProcessingState
        {
            public HistogramProcessingState(Guid plotId, int binCount, int featureIndex, ScaleType axisScaleType, double minValue, double maxValue, int capacity)
            {
                PlotId = plotId;
                Reset(binCount, featureIndex, axisScaleType, minValue, maxValue, capacity);
            }

            public Guid PlotId { get; }
            public int BinCount { get; private set; }
            public int FeatureIndex { get; private set; }
            public ScaleType ScaleType { get; private set; }
            public double MinValue { get; private set; }
            public double MaxValue { get; private set; }
            public double[] Counts { get; private set; } = Array.Empty<double>();
            public int[] RingBins { get; private set; } = Array.Empty<int>();
            public int RingStart { get; set; }
            public int RingCount { get; set; }
            public long LastProcessedSequence { get; set; }

            public bool Matches(int binCount, int featureIndex, ScaleType axisScaleType, double minValue, double maxValue, int capacity)
            {
                return BinCount == binCount
                    && FeatureIndex == featureIndex
                    && ScaleType == axisScaleType
                    && MinValue.Equals(minValue)
                    && MaxValue.Equals(maxValue)
                    && RingBins.Length == capacity;
            }

            public void Reset(int binCount, int featureIndex, ScaleType axisScaleType, double minValue, double maxValue, int capacity)
            {
                BinCount = binCount;
                FeatureIndex = featureIndex;
                ScaleType = axisScaleType;
                MinValue = minValue;
                MaxValue = maxValue;
                Counts = new double[binCount];
                RingBins = new int[capacity];
                RingStart = 0;
                RingCount = 0;
                LastProcessedSequence = 0;
            }

            public void ClearData(long sequence)
            {
                Array.Clear(Counts, 0, Counts.Length);
                RingStart = 0;
                RingCount = 0;
                LastProcessedSequence = sequence;
            }
        }

        private sealed class PseudocolorProcessingState
        {
            public PseudocolorProcessingState(Guid plotId, int binCount, int xFeature, int yFeature, ScaleType xAxisScaleType, ScaleType yAxisScaleType, double minValue, double maxValue, int capacity)
            {
                PlotId = plotId;
                Reset(binCount, xFeature, yFeature, xAxisScaleType, yAxisScaleType, minValue, maxValue, capacity);
            }

            public Guid PlotId { get; }
            public int BinCount { get; private set; }
            public int XFeature { get; private set; }
            public int YFeature { get; private set; }
            public ScaleType XAxisScaleType { get; private set; }
            public ScaleType YAxisScaleType { get; private set; }
            public double MinValue { get; private set; }
            public double MaxValue { get; private set; }
            public int[,] RawCounts { get; private set; } = new int[1, 1];
            public double[,] Normalized { get; private set; } = new double[1, 1];
            public byte[] PixelBuffer { get; private set; } = new byte[4];
            public int PixelWidth { get; private set; } = 1;
            public int PixelHeight { get; private set; } = 1;
            public int[] RingPackedBins { get; private set; } = Array.Empty<int>();
            public int RingStart { get; set; }
            public int RingCount { get; set; }
            public long LastProcessedSequence { get; set; }

            public bool Matches(int binCount, int xFeature, int yFeature, ScaleType xAxisScaleType, ScaleType yAxisScaleType, double minValue, double maxValue, int capacity)
            {
                return BinCount == binCount
                    && XFeature == xFeature
                    && YFeature == yFeature
                    && XAxisScaleType == xAxisScaleType
                    && YAxisScaleType == yAxisScaleType
                    && MinValue.Equals(minValue)
                    && MaxValue.Equals(maxValue)
                    && RingPackedBins.Length == capacity;
            }

            public void Reset(int binCount, int xFeature, int yFeature, ScaleType xAxisScaleType, ScaleType yAxisScaleType, double minValue, double maxValue, int capacity)
            {
                BinCount = binCount;
                XFeature = xFeature;
                YFeature = yFeature;
                XAxisScaleType = xAxisScaleType;
                YAxisScaleType = yAxisScaleType;
                MinValue = minValue;
                MaxValue = maxValue;
                RawCounts = new int[binCount, binCount];
                Normalized = new double[binCount, binCount];
                PixelWidth = binCount;
                PixelHeight = binCount;
                PixelBuffer = new byte[PixelWidth * PixelHeight * 4];
                RingPackedBins = new int[capacity];
                RingStart = 0;
                RingCount = 0;
                LastProcessedSequence = 0;
            }

            public void EnsurePixelBuffer(int pixelWidth, int pixelHeight)
            {
                if (PixelWidth == pixelWidth && PixelHeight == pixelHeight && PixelBuffer.Length == pixelWidth * pixelHeight * 4)
                    return;

                PixelWidth = pixelWidth;
                PixelHeight = pixelHeight;
                PixelBuffer = new byte[pixelWidth * pixelHeight * 4];
            }

            public void ClearData(long sequence)
            {
                Array.Clear(RawCounts, 0, RawCounts.Length);
                Array.Clear(PixelBuffer, 0, PixelBuffer.Length);
                RingStart = 0;
                RingCount = 0;
                LastProcessedSequence = sequence;
            }
        }

        private sealed class SpectralRibbonProcessingState
        {
            public SpectralRibbonProcessingState(Guid plotId, int binCount, int[] channels, int channelHash, ScaleType axisScaleType, double minValue, double maxValue, int capacity)
            {
                PlotId = plotId;
                Reset(binCount, channels, channelHash, axisScaleType, minValue, maxValue, capacity);
            }

            public Guid PlotId { get; }
            public int BinCount { get; private set; }
            public int ChannelHash { get; private set; }
            public ScaleType ScaleType { get; private set; }
            public double MinValue { get; private set; }
            public double MaxValue { get; private set; }
            public int ChannelCount => Channels.Length;
            public int[] Channels { get; private set; } = Array.Empty<int>();
            public int[,] RawCounts { get; private set; } = new int[1, 1];
            public double[,] Normalized { get; private set; } = new double[1, 1];
            public byte[] PixelBuffer { get; private set; } = new byte[4];
            public int PixelWidth { get; private set; } = 1;
            public int PixelHeight { get; private set; } = 1;
            public ushort[,] RingRows { get; private set; } = new ushort[1, 1];
            public int RingStart { get; set; }
            public int RingCount { get; set; }
            public long LastProcessedSequence { get; set; }

            public bool Matches(int binCount, int channelHash, ScaleType axisScaleType, double minValue, double maxValue, int capacity)
            {
                return BinCount == binCount
                    && ChannelHash == channelHash
                    && ScaleType == axisScaleType
                    && MinValue.Equals(minValue)
                    && MaxValue.Equals(maxValue)
                    && RingRows.GetLength(0) == capacity;
            }

            public void Reset(int binCount, int[] channels, int channelHash, ScaleType axisScaleType, double minValue, double maxValue, int capacity)
            {
                BinCount = binCount;
                Channels = channels;
                ChannelHash = channelHash;
                ScaleType = axisScaleType;
                MinValue = minValue;
                MaxValue = maxValue;
                RawCounts = new int[binCount, channels.Length];
                Normalized = new double[binCount, channels.Length];
                PixelWidth = Math.Max(1, channels.Length);
                PixelHeight = binCount;
                PixelBuffer = new byte[PixelWidth * PixelHeight * 4];
                RingRows = new ushort[capacity, channels.Length];
                RingStart = 0;
                RingCount = 0;
                LastProcessedSequence = 0;
            }

            public void EnsurePixelBuffer(int pixelWidth, int pixelHeight)
            {
                if (PixelWidth == pixelWidth && PixelHeight == pixelHeight && PixelBuffer.Length == pixelWidth * pixelHeight * 4)
                    return;

                PixelWidth = pixelWidth;
                PixelHeight = pixelHeight;
                PixelBuffer = new byte[pixelWidth * pixelHeight * 4];
            }

            public void ClearData(long sequence)
            {
                Array.Clear(RawCounts, 0, RawCounts.Length);
                Array.Clear(PixelBuffer, 0, PixelBuffer.Length);
                RingStart = 0;
                RingCount = 0;
                LastProcessedSequence = sequence;
            }
        }
    }
}
