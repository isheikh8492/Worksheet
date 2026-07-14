using System;
using System.Collections.Generic;

using Worksheet.Core.Buffers;
namespace Worksheet.Chasm
{
    public sealed class OscilloscopeBuffer : IAnalogCaptureSink, IOscilloscopeBuffer
    {
        private readonly object _lock = new();
        private readonly Queue<AnalogCapture> _captures;
        private readonly int _capacity;
        private long _version;

        public OscilloscopeBuffer(int capacity = 3)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));

            _capacity = capacity;
            _captures = new Queue<AnalogCapture>(capacity);
        }

        public int Count
        {
            get
            {
                lock (_lock)
                    return _captures.Count;
            }
        }

        public long Version
        {
            get
            {
                lock (_lock)
                    return _version;
            }
        }

        public void Publish(AnalogCapture capture)
        {
            if (capture == null)
                throw new ArgumentNullException(nameof(capture));

            lock (_lock)
            {
                while (_captures.Count >= _capacity)
                    _captures.Dequeue();

                _captures.Enqueue(capture);
                _version++;
            }
        }

        public bool TryGetLatest(out AnalogCapture? capture)
        {
            lock (_lock)
            {
                if (_captures.Count == 0)
                {
                    capture = null;
                    return false;
                }

                capture = null;
                while (_captures.Count > 0)
                    capture = _captures.Dequeue();

                return capture != null;
            }
        }
    }
}
