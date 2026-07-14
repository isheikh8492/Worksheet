namespace Worksheet.Chasm
{
public sealed record ChasmPipeline(
    ChasmEngine ChasmEngine,
    ChasmDataSource ChasmDataSource,
    IProducer Producer,
    IEventIngestionPort? IngestionPort);
}
