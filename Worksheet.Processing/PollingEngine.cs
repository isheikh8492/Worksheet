using System;
using System.Threading;

using Worksheet.Core.Services;
namespace Worksheet.Processing
{
    public abstract class PollingEngine : IDisposable
    {
        private readonly TimeSpan _interval;
        private Timer? _timer;
        private int _isTickRunning;

        protected PollingEngine(TimeSpan interval)
        {
            _interval = interval;
        }

        public bool IsRunning => _timer != null;

        public void Start()
        {
            if (_timer != null)
                return;

            _timer = new Timer(_ => SafeTick(), null, _interval, _interval);
        }

        public void Stop()
        {
            _timer?.Dispose();
            _timer = null;
        }

        public void Dispose()
        {
            Stop();
        }

        protected abstract void Tick();

        private void SafeTick()
        {
            if (Interlocked.Exchange(ref _isTickRunning, 1) == 1)
                return;

            try
            {
                try
                {
                    Tick();
                }
                catch (Exception ex)
                {
                    AppLog.Exception(ex, $"{GetType().Name}.Tick()");
                }
            }
            finally
            {
                Volatile.Write(ref _isTickRunning, 0);
            }
        }
    }
}
