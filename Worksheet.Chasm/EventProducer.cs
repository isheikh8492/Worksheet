using System;
using System.Collections.Generic;
using System.Threading.Channels;
using Worksheet.Core.Models;

using Worksheet.Core.Services;
using Worksheet.Core.Buffers;
namespace Worksheet.Chasm
{
    public sealed class EventProducer : IProducer, IEventIngestionPort
    {
        private readonly Channel<IEventBatch> _channel;
        private readonly EventBatchConverter _converter;
        private readonly SignalLayout _signalLayout;
        private readonly IAnalogCaptureSink? _analogCaptureSink;
        private volatile bool _running;

        public EventProducer(
            ChasmOptions? options = null,
            int maxBatchSize = 1000,
            int parallelCellThreshold = EventBatchConverter.DefaultParallelCellThreshold,
            IAnalogCaptureSink? analogCaptureSink = null)
            : this(
                options?.SignalLayout ?? ChasmOptions.Default.SignalLayout,
                options?.ChannelCapacityBatches ?? ChasmOptions.Default.ChannelCapacityBatches,
                maxBatchSize,
                parallelCellThreshold,
                analogCaptureSink)
        {
        }

        public EventProducer(
            SignalLayout signalLayout,
            int channelCapacityBatches,
            int maxBatchSize = 1000,
            int parallelCellThreshold = EventBatchConverter.DefaultParallelCellThreshold,
            IAnalogCaptureSink? analogCaptureSink = null)
        {
            if (channelCapacityBatches <= 0)
                throw new ArgumentOutOfRangeException(nameof(channelCapacityBatches));

            _signalLayout = signalLayout;
            _analogCaptureSink = analogCaptureSink;

            var bounded = new BoundedChannelOptions(channelCapacityBatches)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false,
            };

            _channel = Channel.CreateBounded<IEventBatch>(bounded);
            _converter = new EventBatchConverter(
                signalLayout,
                maxBatchSize,
                parallelCellThreshold);
        }

        public ChannelReader<IEventBatch> Reader => _channel.Reader;

        public void Start()
        {
            _running = true;
        }

        public void Stop()
        {
            _running = false;

            while (_channel.Reader.TryRead(out _)) { }
        }

        public int Publish(IReadOnlyList<Event> events)
        {
            return PublishEvents(events);
        }

        public int PublishEvents(IReadOnlyList<Event> events)
        {
            if (events == null)
                throw new ArgumentNullException(nameof(events));
            if (!_running || events.Count == 0)
                return 0;

            var batches = _converter.Convert(events);
            PublishAnalogCaptures(events);

            int written = 0;
            foreach (var batch in batches)
            {
                if (_channel.Writer.TryWrite(batch))
                    written++;
            }

            return written;
        }

        public int PublishColumnMajor(double[] values, int eventCount)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));
            if (!_running || eventCount == 0)
                return 0;

            var batch = new ColumnMajorEventBatch(eventCount, values, _signalLayout);
            return _channel.Writer.TryWrite(batch) ? 1 : 0;
        }

        private void PublishAnalogCaptures(IReadOnlyList<Event> events)
        {
            if (_analogCaptureSink == null)
                return;

            try
            {
                for (int i = 0; i < events.Count; i++)
                    _analogCaptureSink.Publish(events[i].AnalogCapture);
            }
            catch (Exception ex)
            {
                AppLog.Exception(ex, "EventProducer.PublishAnalogCaptures");
            }
        }
    }
}
