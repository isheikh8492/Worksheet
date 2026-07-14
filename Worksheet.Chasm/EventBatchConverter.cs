using System;
using System.Collections.Generic;
using System.Threading.Channels;
using System.Threading.Tasks;
using Worksheet.Core.Models;

namespace Worksheet.Chasm
{
    public sealed class EventBatchConverter
    {
        public const int DefaultParallelCellThreshold = 250_000;

        private readonly SignalLayout _signalLayout;

        public EventBatchConverter(
            SignalLayout signalLayout,
            int maxBatchSize = 1000,
            int parallelCellThreshold = DefaultParallelCellThreshold)
        {
            if (maxBatchSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxBatchSize));
            if (parallelCellThreshold < 0)
                throw new ArgumentOutOfRangeException(nameof(parallelCellThreshold));

            _signalLayout = signalLayout;
            MaxBatchSize = maxBatchSize;
            ParallelCellThreshold = parallelCellThreshold;
        }

        public int SignalCount => _signalLayout.SignalCount;

        public int MaxBatchSize { get; }

        public int ParallelCellThreshold { get; }

        public IReadOnlyList<ColumnMajorEventBatch> Convert(IReadOnlyList<Event> events)
        {
            if (events == null)
                throw new ArgumentNullException(nameof(events));
            if (events.Count == 0)
                return Array.Empty<ColumnMajorEventBatch>();

            var batches = new List<ColumnMajorEventBatch>((events.Count + MaxBatchSize - 1) / MaxBatchSize);
            for (int offset = 0; offset < events.Count; offset += MaxBatchSize)
            {
                int count = Math.Min(MaxBatchSize, events.Count - offset);
                batches.Add(ConvertChunk(events, offset, count));
            }

            return batches;
        }

        public int TryWriteTo(ChannelWriter<IEventBatch> writer, IReadOnlyList<Event> events)
        {
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));

            int written = 0;
            foreach (var batch in Convert(events))
            {
                if (!writer.TryWrite(batch))
                    break;

                written++;
            }

            return written;
        }

        private ColumnMajorEventBatch ConvertChunk(IReadOnlyList<Event> events, int offset, int count)
        {
            ValidateEventShape(events, offset, count);

            var values = new double[checked(_signalLayout.SignalCount * count)];
            int cellCount = values.Length;

            if (ParallelCellThreshold > 0 && cellCount >= ParallelCellThreshold)
                FillColumnMajorParallel(events, offset, count, values);
            else
                FillColumnMajor(events, offset, count, values);

            return new ColumnMajorEventBatch(count, values, _signalLayout);
        }

        private void ValidateEventShape(IReadOnlyList<Event> events, int offset, int count)
        {
            for (int e = 0; e < count; e++)
            {
                int signalCount = events[offset + e].SignalCount;
                if (signalCount != _signalLayout.SignalCount)
                {
                    throw new ArgumentException(
                        $"Event at index {offset + e} has {signalCount} signals, expected {_signalLayout.SignalCount}.",
                        nameof(events));
                }
            }
        }

        private void FillColumnMajor(IReadOnlyList<Event> events, int offset, int count, double[] values)
        {
            for (int signal = 0; signal < _signalLayout.SignalCount; signal++)
            {
                int signalOffset = signal * count;
                for (int e = 0; e < count; e++)
                    values[signalOffset + e] = events[offset + e].GetSignalValue(signal);
            }
        }

        private void FillColumnMajorParallel(IReadOnlyList<Event> events, int offset, int count, double[] values)
        {
            Parallel.For(0, _signalLayout.SignalCount, signal =>
            {
                int signalOffset = signal * count;
                for (int e = 0; e < count; e++)
                    values[signalOffset + e] = events[offset + e].GetSignalValue(signal);
            });
        }
    }
}
