using System;
using Worksheet.Core.Models;

using Worksheet.Chasm;
using Worksheet.Core.Buffers;
namespace Worksheet.Chasm
{
    public class DataSource
    {
        private readonly double[][] _channels;
        private readonly object _lock = new();
        private readonly int _signalCount;
        private int _windowCapacity;
        private int _writeIndex;
        private int _count;
        private long _totalEventsIngested;
        private bool _streamingEnabled;
        private long _dataVersion;

        public DataSource(int windowCapacity = 200_000)
            : this(SignalLayout.Default, windowCapacity)
        {
        }

        public DataSource(SignalLayout signalLayout, int windowCapacity = 200_000)
        {
            if (windowCapacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(windowCapacity));

            _signalCount = signalLayout.SignalCount;
            _windowCapacity = windowCapacity;
            _channels = new double[_signalCount][];
            for (int i = 0; i < _signalCount; i++)
                _channels[i] = new double[_windowCapacity];

            _dataVersion = 1;
        }

        private int ClampFeatureIndex(int featureIndex)
        {
            if (featureIndex < 0)
                return 0;
            if (featureIndex >= _channels.Length)
                return _channels.Length - 1;

            return featureIndex;
        }

        public ChannelWindowSnapshot GetSnapshot(int featureIndex)
        {
            int idx = ClampFeatureIndex(featureIndex);
            lock (_lock)
            {
                var metadata = CaptureWindowMetadata();
                return new ChannelWindowSnapshot(
                    Values: _channels[idx],
                    StartIndex: metadata.startIndex,
                    Count: _count,
                    Capacity: _windowCapacity,
                    Version: _dataVersion,
                    StartSequence: metadata.startSequence,
                    EndSequence: _totalEventsIngested);
            }
        }

        public ChannelWindowSnapshot GetSnapshotCopy(int featureIndex)
        {
            int idx = ClampFeatureIndex(featureIndex);
            lock (_lock)
            {
                var metadata = CaptureWindowMetadata();
                var values = new double[_count];
                CopyLogicalWindow(_channels[idx], values, metadata.startIndex, _count);

                return new ChannelWindowSnapshot(
                    Values: values,
                    StartIndex: 0,
                    Count: _count,
                    Capacity: Math.Max(1, values.Length),
                    Version: _dataVersion,
                    StartSequence: metadata.startSequence,
                    EndSequence: _totalEventsIngested);
            }
        }

        public MultiChannelWindowSnapshot GetSnapshot(params int[] featureIndices)
        {
            if (featureIndices == null || featureIndices.Length == 0)
                return MultiChannelWindowSnapshot.Empty;

            lock (_lock)
            {
                var metadata = CaptureWindowMetadata();
                var channels = new double[featureIndices.Length][];
                for (int i = 0; i < featureIndices.Length; i++)
                {
                    int idx = ClampFeatureIndex(featureIndices[i]);
                    channels[i] = _channels[idx];
                }

                return new MultiChannelWindowSnapshot(
                    ChannelValues: channels,
                    StartIndex: metadata.startIndex,
                    Count: _count,
                    Capacity: _windowCapacity,
                    Version: _dataVersion,
                    StartSequence: metadata.startSequence,
                    EndSequence: _totalEventsIngested);
            }
        }

        public MultiChannelWindowSnapshot GetSnapshotCopy(params int[] featureIndices)
        {
            if (featureIndices == null || featureIndices.Length == 0)
                return MultiChannelWindowSnapshot.Empty;

            lock (_lock)
            {
                var metadata = CaptureWindowMetadata();
                var channels = new double[featureIndices.Length][];
                for (int i = 0; i < featureIndices.Length; i++)
                {
                    int idx = ClampFeatureIndex(featureIndices[i]);
                    var values = new double[_count];
                    CopyLogicalWindow(_channels[idx], values, metadata.startIndex, _count);
                    channels[i] = values;
                }

                return new MultiChannelWindowSnapshot(
                    ChannelValues: channels,
                    StartIndex: 0,
                    Count: _count,
                    Capacity: Math.Max(1, _count),
                    Version: _dataVersion,
                    StartSequence: metadata.startSequence,
                    EndSequence: _totalEventsIngested);
            }
        }

        public void SetStreamingEnabled(bool enabled)
        {
            lock (_lock)
            {
                _streamingEnabled = enabled;
            }
        }

        public void ResizeWindow(int windowCapacity)
        {
            if (windowCapacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(windowCapacity));

            lock (_lock)
            {
                if (windowCapacity == _windowCapacity)
                    return;

                int retainedCount = Math.Min(_count, windowCapacity);
                long retainedStartSequence = _totalEventsIngested - retainedCount;

                for (int c = 0; c < _signalCount; c++)
                {
                    var resized = new double[windowCapacity];
                    for (int i = 0; i < retainedCount; i++)
                    {
                        int sourceIndex = PhysicalIndexForSequence(retainedStartSequence + i);
                        resized[i] = _channels[c][sourceIndex];
                    }

                    _channels[c] = resized;
                }

                _windowCapacity = windowCapacity;
                _count = retainedCount;
                _writeIndex = retainedCount % _windowCapacity;
                _dataVersion++;
            }
        }

        public void ClearMemory()
        {
            lock (_lock)
            {
                for (int i = 0; i < _channels.Length; i++)
                    Array.Clear(_channels[i], 0, _channels[i].Length);

                _writeIndex = 0;
                _count = 0;
                _dataVersion++;
            }
        }

        public void AppendBatch(double[][] channels, int count)
        {
            if (channels == null)
                throw new ArgumentNullException(nameof(channels));
            if (channels.Length != _signalCount)
                throw new ArgumentException($"channels must have length {_signalCount}.", nameof(channels));
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            if (count == 0)
                return;

            for (int c = 0; c < _signalCount; c++)
            {
                if (channels[c] == null)
                    throw new ArgumentException($"channels[{c}] is null.", nameof(channels));
                if (channels[c].Length != count)
                    throw new ArgumentException($"channels[{c}] length must equal count.", nameof(channels));
            }

            int sourceOffset = Math.Max(0, count - _windowCapacity);
            int retainedCount = count - sourceOffset;
            if (retainedCount <= 0)
                return;

            lock (_lock)
            {
                for (int c = 0; c < _signalCount; c++)
                    CopyIntoRing(_channels[c], channels[c], sourceOffset, retainedCount);

                _writeIndex = (_writeIndex + retainedCount) % _windowCapacity;
                _count = Math.Min(_count + retainedCount, _windowCapacity);
                _totalEventsIngested += count;
                _dataVersion++;
            }
        }

        public void AppendBatch(ColumnMajorEventBatch batch)
        {
            if (batch == null)
                throw new ArgumentNullException(nameof(batch));

            AppendBatchColumnMajor(batch.Values, batch.Count);
        }

        public void AppendBatchColumnMajor(double[] values, int count)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            if (count == 0)
                return;

            int expectedLength = checked(_signalCount * count);
            if (values.Length != expectedLength)
                throw new ArgumentException($"values must have length {expectedLength}.", nameof(values));

            int sourceOffset = Math.Max(0, count - _windowCapacity);
            int retainedCount = count - sourceOffset;
            if (retainedCount <= 0)
                return;

            lock (_lock)
            {
                for (int c = 0; c < _signalCount; c++)
                {
                    int signalOffset = checked(c * count);
                    CopyIntoRing(_channels[c], values, signalOffset + sourceOffset, retainedCount);
                }

                _writeIndex = (_writeIndex + retainedCount) % _windowCapacity;
                _count = Math.Min(_count + retainedCount, _windowCapacity);
                _totalEventsIngested += count;
                _dataVersion++;
            }
        }

        private void CopyIntoRing(double[] destination, double[] source, int sourceOffset, int count)
        {
            int firstSegment = Math.Min(count, _windowCapacity - _writeIndex);
            Array.Copy(source, sourceOffset, destination, _writeIndex, firstSegment);

            int remaining = count - firstSegment;
            if (remaining > 0)
                Array.Copy(source, sourceOffset + firstSegment, destination, 0, remaining);
        }

        private void CopyLogicalWindow(double[] source, double[] destination, int startIndex, int count)
        {
            if (count <= 0)
                return;

            int firstSegment = Math.Min(count, _windowCapacity - startIndex);
            Array.Copy(source, startIndex, destination, 0, firstSegment);

            int remaining = count - firstSegment;
            if (remaining > 0)
                Array.Copy(source, 0, destination, firstSegment, remaining);
        }

        private (int startIndex, long startSequence) CaptureWindowMetadata()
        {
            int startIndex = (_writeIndex - _count + _windowCapacity) % _windowCapacity;
            long startSequence = _totalEventsIngested - _count;
            return (startIndex, startSequence);
        }

        private int PhysicalIndexForSequence(long sequence)
        {
            long startSequence = _totalEventsIngested - _count;
            int logicalIndex = (int)(sequence - startSequence);
            return (_writeIndex - _count + logicalIndex + _windowCapacity) % _windowCapacity;
        }

        public bool IsStreamingEnabled
        {
            get
            {
                lock (_lock)
                {
                    return _streamingEnabled;
                }
            }
        }

        public long DataVersion
        {
            get
            {
                lock (_lock)
                {
                    return _dataVersion;
                }
            }
        }

        public long TotalEventsIngested
        {
            get
            {
                lock (_lock)
                {
                    return _totalEventsIngested;
                }
            }
        }

        public int BufferedEventCount
        {
            get
            {
                lock (_lock)
                {
                    return _count;
                }
            }
        }

        public int WindowCapacity
        {
            get
            {
                lock (_lock)
                {
                    return _windowCapacity;
                }
            }
        }

        public int SignalCount => _signalCount;
    }
}
