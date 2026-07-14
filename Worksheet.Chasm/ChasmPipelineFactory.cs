using System;

using Worksheet.Core.Services;
namespace Worksheet.Chasm
{
    public static class ChasmPipelineFactory
    {
        public static ChasmPipeline CreateMock(
            DataSource dataSource,
            ChasmOptions? options = null,
            IAnalogCaptureSink? analogCaptureSink = null)
        {
            if (dataSource == null)
                throw new ArgumentNullException(nameof(dataSource));

            var chasmOptions = options ?? ChasmOptions.Default;
            var chasmDataSource = new ChasmDataSource(dataSource);
            var producer = new MockProducer(chasmOptions, analogCaptureSink);
            var consumer = new ChasmConsumer(producer.Reader, chasmDataSource);
            var chasm = new ChasmEngine(producer, consumer, chasmDataSource);

            return new ChasmPipeline(chasm, chasmDataSource, producer, IngestionPort: null);
        }

        public static ChasmPipeline CreateEventIngress(
            DataSource dataSource,
            ChasmOptions? options = null,
            IAnalogCaptureSink? analogCaptureSink = null)
        {
            if (dataSource == null)
                throw new ArgumentNullException(nameof(dataSource));

            var chasmOptions = options ?? ChasmOptions.Default;
            var chasmDataSource = new ChasmDataSource(dataSource);
            var producer = new EventProducer(chasmOptions, analogCaptureSink: analogCaptureSink);
            var consumer = new ChasmConsumer(producer.Reader, chasmDataSource);
            var chasm = new ChasmEngine(producer, consumer, chasmDataSource);

            return new ChasmPipeline(chasm, chasmDataSource, producer, producer);
        }
    }
}
