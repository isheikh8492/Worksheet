using System.Threading;
using System.Threading.Tasks;

namespace Worksheet.Chasm
{
    public interface IConsumer
    {
        Task RunAsync(CancellationToken token);
    }
}

