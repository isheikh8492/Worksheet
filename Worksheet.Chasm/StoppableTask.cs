using System;
using System.Threading.Tasks;

using Worksheet.Core.Services;
namespace Worksheet.Chasm
{
    /// <summary>
    /// Waits for a background loop task to stop, tolerating cancellation and logging a timeout.
    /// </summary>
    internal static class StoppableTask
    {
        public static void Observe(Task? task, TimeSpan timeout, string context)
        {
            if (task == null)
                return;

            try
            {
                if (!task.Wait(timeout))
                    AppLog.Error($"{context} timed out", $"timeoutMs={timeout.TotalMilliseconds:F0}");
            }
            catch (AggregateException ex) when (IsCancellationOnly(ex))
            {
            }
        }

        private static bool IsCancellationOnly(AggregateException ex)
        {
            foreach (var inner in ex.Flatten().InnerExceptions)
            {
                if (inner is not OperationCanceledException)
                    return false;
            }

            return true;
        }
    }
}
