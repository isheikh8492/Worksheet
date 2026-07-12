using System;
using System.Collections.Generic;
using Worksheet.Models;
using Worksheet.Models.Gates;

namespace Worksheet.Services.Viewport.Gates
{
    public sealed class GateProcessor
    {
        private readonly IChannelDataBuffer _buffer;
        private readonly object _stateLock = new();
        private readonly Dictionary<Guid, HistogramGateProcessingState> _histogramStates = new();
        private readonly Dictionary<Guid, PseudocolorGateProcessingState> _pseudocolorStates = new();

        public GateProcessor(IChannelDataBuffer buffer)
        {
            _buffer = buffer;
        }

        public void ResetIncrementalState()
        {
            lock (_stateLock)
            {
                _histogramStates.Clear();
                _pseudocolorStates.Clear();
            }
        }

        public void RemoveInactiveStates(ISet<Guid> activeGateIds)
        {
            lock (_stateLock)
            {
                var staleHistogramIds = new List<Guid>();
                foreach (var gateId in _histogramStates.Keys)
                {
                    if (!activeGateIds.Contains(gateId))
                        staleHistogramIds.Add(gateId);
                }

                foreach (var gateId in staleHistogramIds)
                    _histogramStates.Remove(gateId);

                var stalePseudocolorIds = new List<Guid>();
                foreach (var gateId in _pseudocolorStates.Keys)
                {
                    if (!activeGateIds.Contains(gateId))
                        stalePseudocolorIds.Add(gateId);
                }

                foreach (var gateId in stalePseudocolorIds)
                    _pseudocolorStates.Remove(gateId);
            }
        }

        public GateResult Process(GateSettings gate, PlotSettings plotSettings, long dataVersion, GateProcessorOptions? options = null)
        {
            options ??= new GateProcessorOptions();

            try
            {
                return plotSettings switch
                {
                    HistogramSettings histogram => ProcessHistogram(gate, histogram, dataVersion, options),
                    PseudocolorSettings pseudocolor => ProcessPseudocolor(gate, pseudocolor, dataVersion, options),
                    _ => EmptyResult(gate, plotSettings, dataVersion)
                };
            }
            catch (Exception ex)
            {
                AppLog.Exception(ex, $"GateProcessor.Process gateId={gate.GateId} plotId={plotSettings.Id} plotType={plotSettings.PlotType}");
                return EmptyResult(gate, plotSettings, dataVersion);
            }
        }

        private GateResult ProcessHistogram(GateSettings gate, HistogramSettings settings, long dataVersion, GateProcessorOptions options)
        {
            if (gate.GateType != GateType.Rectangle || gate.Geometry.Type != GateType.Rectangle)
                return EmptyResult(gate, settings, dataVersion);

            int bins = settings.GetBinCount();
            var binGeo = gate.Geometry.ToBinGeometry(bins);
            var mask = BuildRectangleMask1D(bins, binGeo.XMin, binGeo.XMax);

            ChannelWindowSnapshot snapshot = _buffer.GetSnapshot(settings.XFeature);
            int total = snapshot.Count;
            if (total <= 0)
                return EmptyResultWithTotal(gate, settings, dataVersion, total);

            var (scale, offset, isLog, effMin, effMax) = BuildBinTransform(settings, settings.XAxisScaleType);
            if (options.IncludeEventIndices)
                return ProcessHistogramFullScan(gate, settings, dataVersion, snapshot, total, mask, scale, offset, isLog, bins, effMin, effMax);

            int geometryHash = gate.Geometry.GetGeometryHash();
            HistogramGateProcessingState state;
            lock (_stateLock)
            {
                if (!_histogramStates.TryGetValue(gate.GateId, out state!))
                {
                    state = new HistogramGateProcessingState(gate.GateId, geometryHash, settings, snapshot.Capacity);
                    _histogramStates[gate.GateId] = state;
                }
                else if (!state.Matches(geometryHash, settings, snapshot.Capacity))
                {
                    state.Reset(geometryHash, settings, snapshot.Capacity);
                }
            }

            if (NeedsRebuild(state.LastProcessedSequence, snapshot))
            {
                state.ClearData(snapshot.StartSequence);
                ApplyHistogramRange(state, snapshot, snapshot.StartSequence, snapshot.EndSequence, mask, scale, offset, isLog, bins, effMin, effMax);
                state.LastProcessedSequence = snapshot.EndSequence;
            }
            else
            {
                long fromSequence = state.LastProcessedSequence;
                if (fromSequence < snapshot.EndSequence)
                {
                    ApplyHistogramRange(state, snapshot, fromSequence, snapshot.EndSequence, mask, scale, offset, isLog, bins, effMin, effMax);
                    state.LastProcessedSequence = snapshot.EndSequence;
                }

                TrimHistogramToWindow(state, snapshot.Count);
            }

            return new GateResult
            {
                GateId = gate.GateId,
                PlotId = settings.Id,
                DataVersion = dataVersion,
                PassedCount = state.PassedCount,
                TotalCount = total,
                Stats = Build1DStats(state.PassedCount, total, state.Sum, state.SumSq),
            };
        }

        private GateResult ProcessPseudocolor(GateSettings gate, PseudocolorSettings settings, long dataVersion, GateProcessorOptions options)
        {
            int bins = settings.GetBinCount();
            var binGeo = gate.Geometry.ToBinGeometry(bins);
            var mask2d = BuildMask2D(bins, binGeo);

            MultiChannelWindowSnapshot snapshot = _buffer.GetSnapshot(settings.XFeature, settings.YFeature);
            int total = snapshot.Count;
            if (total <= 0)
                return EmptyResultWithTotal(gate, settings, dataVersion, total);

            var (xScale, xOffset, xIsLog, xEffMin, xEffMax) = BuildBinTransform(settings, settings.XAxisScaleType);
            var (yScale, yOffset, yIsLog, yEffMin, yEffMax) = BuildBinTransform(settings, settings.YAxisScaleType);
            if (options.IncludeEventIndices)
                return ProcessPseudocolorFullScan(gate, settings, dataVersion, snapshot, total, mask2d, xScale, xOffset, xIsLog, yScale, yOffset, yIsLog, bins, xEffMin, xEffMax, yEffMin, yEffMax);

            int geometryHash = gate.Geometry.GetGeometryHash();
            PseudocolorGateProcessingState state;
            lock (_stateLock)
            {
                if (!_pseudocolorStates.TryGetValue(gate.GateId, out state!))
                {
                    state = new PseudocolorGateProcessingState(gate.GateId, geometryHash, settings, snapshot.Capacity);
                    _pseudocolorStates[gate.GateId] = state;
                }
                else if (!state.Matches(geometryHash, settings, snapshot.Capacity))
                {
                    state.Reset(geometryHash, settings, snapshot.Capacity);
                }
            }

            if (NeedsRebuild(state.LastProcessedSequence, snapshot))
            {
                state.ClearData(snapshot.StartSequence);
                ApplyPseudocolorRange(state, snapshot, snapshot.StartSequence, snapshot.EndSequence, mask2d, xScale, xOffset, xIsLog, yScale, yOffset, yIsLog, bins, xEffMin, xEffMax, yEffMin, yEffMax);
                state.LastProcessedSequence = snapshot.EndSequence;
            }
            else
            {
                long fromSequence = state.LastProcessedSequence;
                if (fromSequence < snapshot.EndSequence)
                {
                    ApplyPseudocolorRange(state, snapshot, fromSequence, snapshot.EndSequence, mask2d, xScale, xOffset, xIsLog, yScale, yOffset, yIsLog, bins, xEffMin, xEffMax, yEffMin, yEffMax);
                    state.LastProcessedSequence = snapshot.EndSequence;
                }

                TrimPseudocolorToWindow(state, snapshot.Count);
            }

            return new GateResult
            {
                GateId = gate.GateId,
                PlotId = settings.Id,
                DataVersion = dataVersion,
                PassedCount = state.PassedCount,
                TotalCount = total,
                Stats = Build2DStats(state.PassedCount, total, state.SumX, state.SumSqX, state.SumY, state.SumSqY),
            };
        }

        private static GateResult ProcessHistogramFullScan(
            GateSettings gate,
            PlotSettings settings,
            long dataVersion,
            ChannelWindowSnapshot snapshot,
            int total,
            bool[] mask,
            double scale,
            double offset,
            bool isLog,
            int bins,
            double effMin,
            double effMax)
        {
            int passed = 0;
            double sum = 0;
            double sumsq = 0;
            var indices = new List<int>();

            RunSequential(snapshot, (physicalIndex, logicalIndex) =>
            {
                double value = snapshot.Values[physicalIndex];
                if (!double.IsFinite(value))
                    return;

                int xBin = ToBin(value, scale, offset, isLog, bins, effMin, effMax);
                if (!mask[xBin])
                    return;

                passed++;
                sum += value;
                sumsq += value * value;
                indices.Add(logicalIndex);
            });

            return new GateResult
            {
                GateId = gate.GateId,
                PlotId = settings.Id,
                DataVersion = dataVersion,
                PassedCount = passed,
                TotalCount = total,
                Stats = Build1DStats(passed, total, sum, sumsq),
                EventIndices = indices.ToArray(),
            };
        }

        private static GateResult ProcessPseudocolorFullScan(
            GateSettings gate,
            PlotSettings settings,
            long dataVersion,
            MultiChannelWindowSnapshot snapshot,
            int total,
            bool[,] mask2d,
            double xScale,
            double xOffset,
            bool xIsLog,
            double yScale,
            double yOffset,
            bool yIsLog,
            int bins,
            double xEffMin,
            double xEffMax,
            double yEffMin,
            double yEffMax)
        {
            int passed = 0;
            double sumX = 0;
            double sumsqX = 0;
            double sumY = 0;
            double sumsqY = 0;
            var indices = new List<int>();

            RunSequential(snapshot, total, (xPhysicalIndex, yPhysicalIndex, logicalIndex) =>
            {
                double xv = snapshot.ChannelValues[0][xPhysicalIndex];
                double yv = snapshot.ChannelValues[1][yPhysicalIndex];
                if (!double.IsFinite(xv) || !double.IsFinite(yv))
                    return;

                int xBin = ToBin(xv, xScale, xOffset, xIsLog, bins, xEffMin, xEffMax);
                int yBin = ToBin(yv, yScale, yOffset, yIsLog, bins, yEffMin, yEffMax);
                if (!mask2d[yBin, xBin])
                    return;

                passed++;
                sumX += xv;
                sumsqX += xv * xv;
                sumY += yv;
                sumsqY += yv * yv;
                indices.Add(logicalIndex);
            });

            return new GateResult
            {
                GateId = gate.GateId,
                PlotId = settings.Id,
                DataVersion = dataVersion,
                PassedCount = passed,
                TotalCount = total,
                Stats = Build2DStats(passed, total, sumX, sumsqX, sumY, sumsqY),
                EventIndices = indices.ToArray(),
            };
        }

        private static GateStatistics Build1DStats(int passed, int total, double sum, double sumsq)
        {
            if (passed <= 0 || total <= 0)
            {
                return new GateStatistics
                {
                    Percent = 0,
                    Total = 0,
                    X = new GateAxisStatistics(0, 0, 0, 0),
                };
            }

            double mean = sum / passed;
            double var = sumsq / passed - mean * mean;
            if (var < 0) var = 0;
            double std = Math.Sqrt(var);
            double cv = mean > 0 ? (std / mean) * 100 : 0;
            double percent = passed * 100.0 / total;

            return new GateStatistics
            {
                Percent = percent,
                Total = passed,
                X = new GateAxisStatistics(mean, std, var, cv),
            };
        }

        private static GateStatistics Build2DStats(int passed, int total, double sumX, double sumsqX, double sumY, double sumsqY)
        {
            if (passed <= 0 || total <= 0)
            {
                return new GateStatistics
                {
                    Percent = 0,
                    Total = 0,
                    X = new GateAxisStatistics(0, 0, 0, 0),
                    Y = new GateAxisStatistics(0, 0, 0, 0),
                };
            }

            double meanX = sumX / passed;
            double varX = sumsqX / passed - meanX * meanX;
            if (varX < 0) varX = 0;
            double stdX = Math.Sqrt(varX);
            double cvX = meanX > 0 ? (stdX / meanX) * 100 : 0;

            double meanY = sumY / passed;
            double varY = sumsqY / passed - meanY * meanY;
            if (varY < 0) varY = 0;
            double stdY = Math.Sqrt(varY);
            double cvY = meanY > 0 ? (stdY / meanY) * 100 : 0;

            double percent = passed * 100.0 / total;

            return new GateStatistics
            {
                Percent = percent,
                Total = passed,
                X = new GateAxisStatistics(meanX, stdX, varX, cvX),
                Y = new GateAxisStatistics(meanY, stdY, varY, cvY),
            };
        }

        private static GateResult EmptyResult(GateSettings gate, PlotSettings settings, long dataVersion) =>
            new()
            {
                GateId = gate.GateId,
                PlotId = settings.Id,
                DataVersion = dataVersion,
                PassedCount = 0,
                TotalCount = 0,
                Stats = new GateStatistics
                {
                    Percent = 0,
                    Total = 0,
                }
            };

        private static GateResult EmptyResultWithTotal(GateSettings gate, PlotSettings settings, long dataVersion, int total) =>
            new()
            {
                GateId = gate.GateId,
                PlotId = settings.Id,
                DataVersion = dataVersion,
                PassedCount = 0,
                TotalCount = total,
                Stats = new GateStatistics
                {
                    Percent = 0,
                    Total = 0,
                    X = new GateAxisStatistics(0, 0, 0, 0),
                    Y = settings.PlotType == PlotType.Pseudocolor ? new GateAxisStatistics(0, 0, 0, 0) : null,
                }
            };

        private static void ApplyHistogramRange(
            HistogramGateProcessingState state,
            ChannelWindowSnapshot snapshot,
            long fromSequence,
            long toSequence,
            bool[] mask,
            double scale,
            double offset,
            bool isLog,
            int bins,
            double effMin,
            double effMax)
        {
            for (long sequence = fromSequence; sequence < toSequence; sequence++)
            {
                if (state.RingCount == state.RingPassed.Length)
                    EvictOldestHistogramContribution(state);

                int physicalIndex = snapshot.PhysicalIndexForSequence(sequence);
                double value = snapshot.Values[physicalIndex];
                bool passed = false;

                if (double.IsFinite(value))
                {
                    int xBin = ToBin(value, scale, offset, isLog, bins, effMin, effMax);
                    passed = mask[xBin];
                }

                int writeIndex = (state.RingStart + state.RingCount) % state.RingPassed.Length;
                state.RingPassed[writeIndex] = passed;
                state.RingValues[writeIndex] = value;
                state.RingCount++;

                if (!passed)
                    continue;

                state.PassedCount++;
                state.Sum += value;
                state.SumSq += value * value;
            }
        }

        private static void EvictOldestHistogramContribution(HistogramGateProcessingState state)
        {
            int evictIndex = state.RingStart;
            if (state.RingPassed[evictIndex])
            {
                double value = state.RingValues[evictIndex];
                state.PassedCount--;
                state.Sum -= value;
                state.SumSq -= value * value;
            }

            state.RingStart = (state.RingStart + 1) % state.RingPassed.Length;
            state.RingCount--;
        }

        private static void TrimHistogramToWindow(HistogramGateProcessingState state, int windowCount)
        {
            while (state.RingCount > windowCount)
                EvictOldestHistogramContribution(state);
        }

        private static void ApplyPseudocolorRange(
            PseudocolorGateProcessingState state,
            MultiChannelWindowSnapshot snapshot,
            long fromSequence,
            long toSequence,
            bool[,] mask2d,
            double xScale,
            double xOffset,
            bool xIsLog,
            double yScale,
            double yOffset,
            bool yIsLog,
            int bins,
            double xEffMin,
            double xEffMax,
            double yEffMin,
            double yEffMax)
        {
            for (long sequence = fromSequence; sequence < toSequence; sequence++)
            {
                if (state.RingCount == state.RingPassed.Length)
                    EvictOldestPseudocolorContribution(state);

                int physicalIndex = snapshot.PhysicalIndexForSequence(sequence);
                double xv = snapshot.ChannelValues[0][physicalIndex];
                double yv = snapshot.ChannelValues[1][physicalIndex];
                bool passed = false;

                if (double.IsFinite(xv) && double.IsFinite(yv))
                {
                    int xBin = ToBin(xv, xScale, xOffset, xIsLog, bins, xEffMin, xEffMax);
                    int yBin = ToBin(yv, yScale, yOffset, yIsLog, bins, yEffMin, yEffMax);
                    passed = mask2d[yBin, xBin];
                }

                int writeIndex = (state.RingStart + state.RingCount) % state.RingPassed.Length;
                state.RingPassed[writeIndex] = passed;
                state.RingXValues[writeIndex] = xv;
                state.RingYValues[writeIndex] = yv;
                state.RingCount++;

                if (!passed)
                    continue;

                state.PassedCount++;
                state.SumX += xv;
                state.SumSqX += xv * xv;
                state.SumY += yv;
                state.SumSqY += yv * yv;
            }
        }

        private static void EvictOldestPseudocolorContribution(PseudocolorGateProcessingState state)
        {
            int evictIndex = state.RingStart;
            if (state.RingPassed[evictIndex])
            {
                double xv = state.RingXValues[evictIndex];
                double yv = state.RingYValues[evictIndex];
                state.PassedCount--;
                state.SumX -= xv;
                state.SumSqX -= xv * xv;
                state.SumY -= yv;
                state.SumSqY -= yv * yv;
            }

            state.RingStart = (state.RingStart + 1) % state.RingPassed.Length;
            state.RingCount--;
        }

        private static void TrimPseudocolorToWindow(PseudocolorGateProcessingState state, int windowCount)
        {
            while (state.RingCount > windowCount)
                EvictOldestPseudocolorContribution(state);
        }

        private static bool[] BuildRectangleMask1D(int bins, double xMin, double xMax)
        {
            var mask = new bool[bins];

            int start = (int)Math.Ceiling(xMin - 0.5);
            int end = (int)Math.Floor(xMax - 0.5);
            start = Math.Clamp(start, 0, bins - 1);
            end = Math.Clamp(end, 0, bins - 1);
            if (start > end)
                return mask;

            for (int x = start; x <= end; x++)
                mask[x] = true;

            return mask;
        }

        private static bool[,] BuildMask2D(int bins, GateBinGeometry geo)
        {
            var mask = new bool[bins, bins];

            switch (geo.Type)
            {
                case GateType.Rectangle:
                    FillRectangle(mask, bins, geo.XMin, geo.XMax, geo.YMin, geo.YMax);
                    break;
                case GateType.Ellipse:
                    FillEllipse(mask, bins, geo.CenterX, geo.CenterY, geo.RadiusX, geo.RadiusY, geo.AngleDeg);
                    break;
                case GateType.Polygon:
                    FillPolygon(mask, bins, geo.PolygonPoints);
                    break;
            }

            return mask;
        }

        private static void FillRectangle(bool[,] mask, int bins, double xMin, double xMax, double yMin, double yMax)
        {
            int xStart = (int)Math.Ceiling(xMin - 0.5);
            int xEnd = (int)Math.Floor(xMax - 0.5);
            int yStart = (int)Math.Ceiling(yMin - 0.5);
            int yEnd = (int)Math.Floor(yMax - 0.5);

            xStart = Math.Clamp(xStart, 0, bins - 1);
            xEnd = Math.Clamp(xEnd, 0, bins - 1);
            yStart = Math.Clamp(yStart, 0, bins - 1);
            yEnd = Math.Clamp(yEnd, 0, bins - 1);

            if (xStart > xEnd || yStart > yEnd)
                return;

            for (int y = yStart; y <= yEnd; y++)
                for (int x = xStart; x <= xEnd; x++)
                    mask[y, x] = true;
        }

        private static void FillEllipse(bool[,] mask, int bins, double cx, double cy, double rx, double ry, double angleDeg)
        {
            if (rx <= 0 || ry <= 0)
                return;

            double angleRad = angleDeg * Math.PI / 180.0;
            double cos = Math.Cos(-angleRad);
            double sin = Math.Sin(-angleRad);
            double invRx2 = 1.0 / (rx * rx);
            double invRy2 = 1.0 / (ry * ry);

            for (int y = 0; y < bins; y++)
            {
                double py = y + 0.5;
                for (int x = 0; x < bins; x++)
                {
                    double px = x + 0.5;

                    double dx = px - cx;
                    double dy = py - cy;
                    double xRot = dx * cos - dy * sin;
                    double yRot = dx * sin + dy * cos;

                    double v = xRot * xRot * invRx2 + yRot * yRot * invRy2;
                    if (v <= 1.0)
                        mask[y, x] = true;
                }
            }
        }

        private static void FillPolygon(bool[,] mask, int bins, IReadOnlyList<GateBinPoint>? points)
        {
            if (points == null || points.Count < 3)
                return;

            for (int y = 0; y < bins; y++)
            {
                double py = y + 0.5;
                for (int x = 0; x < bins; x++)
                {
                    double px = x + 0.5;
                    if (PointInPolygon(px, py, points))
                        mask[y, x] = true;
                }
            }
        }

        private static bool PointInPolygon(double x, double y, IReadOnlyList<GateBinPoint> poly)
        {
            bool inside = false;
            int j = poly.Count - 1;
            for (int i = 0; i < poly.Count; i++)
            {
                double xi = poly[i].X;
                double yi = poly[i].Y;
                double xj = poly[j].X;
                double yj = poly[j].Y;

                bool intersect = ((yi > y) != (yj > y)) &&
                                 (x < (xj - xi) * (y - yi) / (yj - yi + 1e-12) + xi);
                if (intersect)
                    inside = !inside;
                j = i;
            }

            return inside;
        }

        private static (double scale, double offset, bool isLog, double effMin, double effMax) BuildBinTransform(
            ParameterPlotSettings settings, AxisScaleType scaleType)
        {
            double min = settings.MinValue;
            double max = settings.MaxValue;
            int bins = settings.GetBinCount();

            if (scaleType == AxisScaleType.Logarithmic)
            {
                if (min < 1) min = 1;
                if (max <= min) max = min * 10;
                double minLog = Math.Log10(min);
                double maxLog = Math.Log10(max);
                double scale = bins / (maxLog - minLog);
                double offset = -minLog * scale;
                return (scale, offset, true, min, max);
            }

            if (max <= min) max = min + 1;
            double linearScale = bins / (max - min);
            double linearOffset = -min * linearScale;
            return (linearScale, linearOffset, false, min, max);
        }

        private static int ToBin(double value, double scale, double offset, bool isLog,
            int bins, double effMin, double effMax)
        {
            if (value < effMin) value = effMin;
            else if (value > effMax) value = effMax;

            double pos = isLog ? Math.Log10(value) * scale + offset : value * scale + offset;
            return Math.Clamp((int)pos, 0, bins - 1);
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

        private static void RunSequential(ChannelWindowSnapshot snapshot, Action<int, int> action)
        {
            if (snapshot.Count <= 0)
                return;

            if (snapshot.IsContiguous)
            {
                int end = snapshot.StartIndex + snapshot.Count;
                int logicalIndex = 0;
                for (int physicalIndex = snapshot.StartIndex; physicalIndex < end; physicalIndex++, logicalIndex++)
                    action(physicalIndex, logicalIndex);
                return;
            }

            for (int logicalIndex = 0; logicalIndex < snapshot.Count; logicalIndex++)
                action(snapshot.PhysicalIndexAt(logicalIndex), logicalIndex);
        }

        private static void RunSequential(MultiChannelWindowSnapshot snapshot, int count, Action<int, int, int> action)
        {
            if (count <= 0)
                return;

            if (snapshot.IsContiguous)
            {
                int end = snapshot.StartIndex + count;
                int xPhysicalIndex = snapshot.StartIndex;
                int yPhysicalIndex = snapshot.StartIndex;
                int logicalIndex = 0;

                while (xPhysicalIndex < end)
                {
                    action(xPhysicalIndex, yPhysicalIndex, logicalIndex);
                    xPhysicalIndex++;
                    yPhysicalIndex++;
                    logicalIndex++;
                }

                return;
            }

            for (int logicalIndex = 0; logicalIndex < count; logicalIndex++)
            {
                int physicalIndex = snapshot.PhysicalIndexAt(logicalIndex);
                action(physicalIndex, physicalIndex, logicalIndex);
            }
        }

        private sealed class HistogramGateProcessingState
        {
            public HistogramGateProcessingState(Guid gateId, int geometryHash, HistogramSettings settings, int capacity)
            {
                GateId = gateId;
                Reset(geometryHash, settings, capacity);
            }

            public Guid GateId { get; }
            public int GeometryHash { get; private set; }
            public int BinCount { get; private set; }
            public int FeatureIndex { get; private set; }
            public AxisScaleType AxisScaleType { get; private set; }
            public double MinValue { get; private set; }
            public double MaxValue { get; private set; }
            public bool[] RingPassed { get; private set; } = Array.Empty<bool>();
            public double[] RingValues { get; private set; } = Array.Empty<double>();
            public int RingStart { get; set; }
            public int RingCount { get; set; }
            public long LastProcessedSequence { get; set; }
            public int PassedCount { get; set; }
            public double Sum { get; set; }
            public double SumSq { get; set; }

            public bool Matches(int geometryHash, HistogramSettings settings, int capacity)
            {
                return GeometryHash == geometryHash
                    && BinCount == settings.GetBinCount()
                    && FeatureIndex == settings.XFeature
                    && AxisScaleType == settings.XAxisScaleType
                    && MinValue.Equals(settings.MinValue)
                    && MaxValue.Equals(settings.MaxValue)
                    && RingPassed.Length == capacity;
            }

            public void Reset(int geometryHash, HistogramSettings settings, int capacity)
            {
                GeometryHash = geometryHash;
                BinCount = settings.GetBinCount();
                FeatureIndex = settings.XFeature;
                AxisScaleType = settings.XAxisScaleType;
                MinValue = settings.MinValue;
                MaxValue = settings.MaxValue;
                RingPassed = new bool[capacity];
                RingValues = new double[capacity];
                RingStart = 0;
                RingCount = 0;
                LastProcessedSequence = 0;
                PassedCount = 0;
                Sum = 0;
                SumSq = 0;
            }

            public void ClearData(long sequence)
            {
                RingStart = 0;
                RingCount = 0;
                LastProcessedSequence = sequence;
                PassedCount = 0;
                Sum = 0;
                SumSq = 0;
            }
        }

        private sealed class PseudocolorGateProcessingState
        {
            public PseudocolorGateProcessingState(Guid gateId, int geometryHash, PseudocolorSettings settings, int capacity)
            {
                GateId = gateId;
                Reset(geometryHash, settings, capacity);
            }

            public Guid GateId { get; }
            public int GeometryHash { get; private set; }
            public int BinCount { get; private set; }
            public int XFeature { get; private set; }
            public int YFeature { get; private set; }
            public AxisScaleType XAxisScaleType { get; private set; }
            public AxisScaleType YAxisScaleType { get; private set; }
            public double MinValue { get; private set; }
            public double MaxValue { get; private set; }
            public bool[] RingPassed { get; private set; } = Array.Empty<bool>();
            public double[] RingXValues { get; private set; } = Array.Empty<double>();
            public double[] RingYValues { get; private set; } = Array.Empty<double>();
            public int RingStart { get; set; }
            public int RingCount { get; set; }
            public long LastProcessedSequence { get; set; }
            public int PassedCount { get; set; }
            public double SumX { get; set; }
            public double SumSqX { get; set; }
            public double SumY { get; set; }
            public double SumSqY { get; set; }

            public bool Matches(int geometryHash, PseudocolorSettings settings, int capacity)
            {
                return GeometryHash == geometryHash
                    && BinCount == settings.GetBinCount()
                    && XFeature == settings.XFeature
                    && YFeature == settings.YFeature
                    && XAxisScaleType == settings.XAxisScaleType
                    && YAxisScaleType == settings.YAxisScaleType
                    && MinValue.Equals(settings.MinValue)
                    && MaxValue.Equals(settings.MaxValue)
                    && RingPassed.Length == capacity;
            }

            public void Reset(int geometryHash, PseudocolorSettings settings, int capacity)
            {
                GeometryHash = geometryHash;
                BinCount = settings.GetBinCount();
                XFeature = settings.XFeature;
                YFeature = settings.YFeature;
                XAxisScaleType = settings.XAxisScaleType;
                YAxisScaleType = settings.YAxisScaleType;
                MinValue = settings.MinValue;
                MaxValue = settings.MaxValue;
                RingPassed = new bool[capacity];
                RingXValues = new double[capacity];
                RingYValues = new double[capacity];
                RingStart = 0;
                RingCount = 0;
                LastProcessedSequence = 0;
                PassedCount = 0;
                SumX = 0;
                SumSqX = 0;
                SumY = 0;
                SumSqY = 0;
            }

            public void ClearData(long sequence)
            {
                RingStart = 0;
                RingCount = 0;
                LastProcessedSequence = sequence;
                PassedCount = 0;
                SumX = 0;
                SumSqX = 0;
                SumY = 0;
                SumSqY = 0;
            }
        }
    }
}
