using Worksheet.Core.Buffers;
namespace Worksheet.Chasm
{
    public interface IAnalogCaptureSink
    {
        void Publish(AnalogCapture capture);
    }
}
