using Worksheet.Core.Models;
using Worksheet.Core.Models.Gates;
using Worksheet.Core.Services;
using Worksheet.Processing;
using Worksheet.Chasm;
using Xunit;

using Worksheet.Processing.Gates;
namespace Worksheet.Tests;

public sealed class GateProcessorTests
{
    // bins=10 over [0,10] linear => value v maps to bin (int)v, so mid-bin values (v.5) are unambiguous.
    private const int Bins = 10;

    [Fact]
    public void HistogramRectangleGate_CountsInsideAcrossFullRebuildAndDelta()
    {
        var settings = new HistogramSettings
        {
            BinCount = Bins,
            XFeature = 0,
            XAxisScaleType = ScaleType.Linear,
            MinValue = 0,
            MaxValue = Bins,
        };
        // X gate covers bins [3,6) -> bins 3,4,5 pass.
        var gate = RectangleGate(settings.Id, 0.3, 0.6, 0.0, 1.0);

        var source = new DataSource(windowCapacity: 100);
        var processor = new GateProcessor(new ChasmDataSource(source));

        // Frame 1 (full rebuild): 3 inside (3.5,4.5,5.5) + 2 outside (0.5,1.5).
        Append(source, new[] { 3.5, 4.5, 5.5, 0.5, 1.5 });
        var r1 = processor.Process(gate, settings, dataVersion: 1);
        Assert.Equal(5, r1.TotalCount);
        Assert.Equal(3, r1.PassedCount);
        Assert.Equal(60.0, r1.Stats.Percent, 6);

        // Frame 2 (delta over the 3 new events): 1 more inside (4.5), 2 outside (8.5,9.5).
        Append(source, new[] { 8.5, 9.5, 4.5 });
        var r2 = processor.Process(gate, settings, dataVersion: 2);
        Assert.Equal(8, r2.TotalCount);
        Assert.Equal(4, r2.PassedCount);
        Assert.Equal(50.0, r2.Stats.Percent, 6);
    }

    [Fact]
    public void PseudocolorRectangleGate_CountsInsideAcrossFullRebuildAndDelta()
    {
        var settings = new PseudocolorSettings
        {
            BinCount = Bins,
            XFeature = 0,
            YFeature = 1,
            XAxisScaleType = ScaleType.Linear,
            YAxisScaleType = ScaleType.Linear,
            MinValue = 0,
            MaxValue = Bins,
        };
        // Gate covers bins X[3,6) and Y[3,6): passes only when both xBin and yBin in {3,4,5}.
        var gate = RectangleGate(settings.Id, 0.3, 0.6, 0.3, 0.6);

        var source = new DataSource(windowCapacity: 100);
        var processor = new GateProcessor(new ChasmDataSource(source));

        // Frame 1: (4.5,4.5)=in, (4.5,9.5)=out(y), (0.5,4.5)=out(x), (5.5,3.5)=in  => 2 pass.
        Append(source, new[] { 4.5, 4.5, 0.5, 5.5 }, new[] { 4.5, 9.5, 4.5, 3.5 });
        var r1 = processor.Process(gate, settings, dataVersion: 1);
        Assert.Equal(4, r1.TotalCount);
        Assert.Equal(2, r1.PassedCount);

        // Frame 2 (delta): (3.5,5.5)=in, (8.5,8.5)=out => 1 more pass.
        Append(source, new[] { 3.5, 8.5 }, new[] { 5.5, 8.5 });
        var r2 = processor.Process(gate, settings, dataVersion: 2);
        Assert.Equal(6, r2.TotalCount);
        Assert.Equal(3, r2.PassedCount);
    }

    private static GateSettings RectangleGate(System.Guid plotId, double xMin01, double xMax01, double yMin01, double yMax01) =>
        new()
        {
            Plot = new GatePlotRef(plotId),
            GateType = GateType.Rectangle,
            Geometry = GateGeometry.Rectangle01(xMin01, xMax01, yMin01, yMax01),
        };

    private static void Append(DataSource source, double[] channel0, double[]? channel1 = null)
    {
        int count = channel0.Length;
        var batch = new double[SignalLayout.Default.SignalCount][];
        for (int c = 0; c < batch.Length; c++)
            batch[c] = new double[count];

        for (int e = 0; e < count; e++)
            batch[0][e] = channel0[e];

        if (channel1 != null)
            for (int e = 0; e < count; e++)
                batch[1][e] = channel1[e];

        source.AppendBatch(batch, count);
    }
}
