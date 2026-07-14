using System;
using System.Collections.Generic;
using Worksheet.Core.Models;

namespace Worksheet.Processing
{
    public sealed class PlotPipelineRegistry
    {
        private static readonly int PlotTypeCount = Enum.GetValues<PlotType>().Length;
        private readonly IPlotPipeline?[] _pipelines = new IPlotPipeline?[PlotTypeCount];

        public TimeSpan FastestCadence
        {
            get
            {
                TimeSpan? fastest = null;
                for (int i = 0; i < _pipelines.Length; i++)
                {
                    var pipeline = _pipelines[i];
                    if (pipeline == null)
                        continue;

                    if (fastest == null || pipeline.Cadence < fastest.Value)
                        fastest = pipeline.Cadence;
                }

                return fastest ?? throw new InvalidOperationException("No plot pipelines are registered.");
            }
        }

        public void Register(PlotType plotType, IPlotPipeline pipeline)
        {
            ArgumentNullException.ThrowIfNull(pipeline);

            int index = ToIndex(plotType);
            if (_pipelines[index] != null)
                throw new InvalidOperationException($"A plot pipeline is already registered for {plotType}.");

            _pipelines[index] = pipeline;
        }

        public IPlotPipeline GetRequired(PlotType plotType)
        {
            var pipeline = _pipelines[ToIndex(plotType)];
            return pipeline ?? throw new InvalidOperationException($"No plot pipeline is registered for {plotType}.");
        }

        public void ResetStates()
        {
            foreach (var pipeline in GetUniquePipelines())
                pipeline.ResetState();
        }

        public (long deltaAppliedCount, long fullRebuildCount, long sequenceGapCount) GetDeltaStats()
        {
            long deltaAppliedCount = 0;
            long fullRebuildCount = 0;
            long sequenceGapCount = 0;

            foreach (var pipeline in GetUniquePipelines())
            {
                var stats = pipeline.GetDeltaStats();
                deltaAppliedCount += stats.deltaAppliedCount;
                fullRebuildCount += stats.fullRebuildCount;
                sequenceGapCount += stats.sequenceGapCount;
            }

            return (deltaAppliedCount, fullRebuildCount, sequenceGapCount);
        }

        private IEnumerable<IPlotPipeline> GetUniquePipelines()
        {
            var seen = new HashSet<IPlotPipeline>();
            for (int i = 0; i < _pipelines.Length; i++)
            {
                var pipeline = _pipelines[i];
                if (pipeline != null && seen.Add(pipeline))
                    yield return pipeline;
            }
        }

        private static int ToIndex(PlotType plotType)
        {
            int index = (int)plotType;
            if (index < 0 || index >= PlotTypeCount)
                throw new ArgumentOutOfRangeException(nameof(plotType), plotType, "Unsupported plot type.");

            return index;
        }
    }
}
