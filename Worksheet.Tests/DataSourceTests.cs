using System;
using Worksheet.Core.Models;
using Worksheet.Core.Services;
using Worksheet.Processing;
using Worksheet.Chasm;
using Xunit;

namespace Worksheet.Tests;

public sealed class DataSourceTests
{
    [Fact]
    public void AppendBatchStoresSelectedChannelValuesInSequenceOrder()
    {
        var source = new DataSource(windowCapacity: 5);
        var batch = CreateBatch(count: 3);

        batch[7][0] = 70;
        batch[7][1] = 71;
        batch[7][2] = 72;

        source.AppendBatch(batch, count: 3);

        var snapshot = source.GetSnapshot(7);

        Assert.Equal(3, source.BufferedEventCount);
        Assert.Equal(3, snapshot.Count);
        Assert.Equal(0, snapshot.StartSequence);
        Assert.Equal(3, snapshot.EndSequence);
        Assert.Equal(70, snapshot.Values[snapshot.PhysicalIndexForSequence(0)]);
        Assert.Equal(71, snapshot.Values[snapshot.PhysicalIndexForSequence(1)]);
        Assert.Equal(72, snapshot.Values[snapshot.PhysicalIndexForSequence(2)]);
    }

    [Fact]
    public void AppendBatchRetainsOnlyRollingWindowWhenCapacityIsExceeded()
    {
        var source = new DataSource(windowCapacity: 3);
        var first = CreateBatch(count: 2);
        var second = CreateBatch(count: 3);

        first[2][0] = 20;
        first[2][1] = 21;
        second[2][0] = 22;
        second[2][1] = 23;
        second[2][2] = 24;

        source.AppendBatch(first, count: 2);
        source.AppendBatch(second, count: 3);

        var snapshot = source.GetSnapshot(2);

        Assert.Equal(3, source.BufferedEventCount);
        Assert.Equal(3, snapshot.Count);
        Assert.Equal(2, snapshot.StartSequence);
        Assert.Equal(5, snapshot.EndSequence);
        Assert.Equal(22, snapshot.Values[snapshot.PhysicalIndexForSequence(2)]);
        Assert.Equal(23, snapshot.Values[snapshot.PhysicalIndexForSequence(3)]);
        Assert.Equal(24, snapshot.Values[snapshot.PhysicalIndexForSequence(4)]);
    }

    [Fact]
    public void EventBatchRejectsWrongChannelCount()
    {
        var channels = new double[59][];
        for (int i = 0; i < channels.Length; i++)
            channels[i] = new double[1];

        Assert.Throws<ArgumentException>(() => new EventBatch(1, channels));
    }

    [Fact]
    public void DataSourceReadsSelectedLaserFeatureChannelColumn()
    {
        var layout = new SignalLayout(6, 9, 60);
        int signalIndex = layout.ToIndex(2, 4, 17);
        var source = new DataSource(layout, windowCapacity: 4);
        var batch = CreateBatch(count: 4, signalCount: layout.SignalCount);

        batch[signalIndex][0] = 1_337;
        batch[signalIndex][1] = 1_338;
        batch[signalIndex][2] = 1_339;
        batch[signalIndex][3] = 1_340;

        source.AppendBatch(batch, count: 4);

        var snapshot = source.GetSnapshot(signalIndex);

        Assert.Equal(layout.SignalCount, source.SignalCount);
        Assert.Equal(1_337, snapshot.Values[snapshot.PhysicalIndexForSequence(0)]);
        Assert.Equal(1_338, snapshot.Values[snapshot.PhysicalIndexForSequence(1)]);
        Assert.Equal(1_339, snapshot.Values[snapshot.PhysicalIndexForSequence(2)]);
        Assert.Equal(1_340, snapshot.Values[snapshot.PhysicalIndexForSequence(3)]);
    }

    [Fact]
    public void AppendColumnMajorBatchStoresSelectedLaserFeatureChannelColumn()
    {
        var layout = new SignalLayout(6, 9, 60);
        int signalIndex = layout.ToIndex(2, 4, 17);
        var source = new DataSource(layout, windowCapacity: 4);
        var values = CreateColumnMajorBatch(layout.SignalCount, count: 4);

        values[(signalIndex * 4) + 0] = 1_337;
        values[(signalIndex * 4) + 1] = 1_338;
        values[(signalIndex * 4) + 2] = 1_339;
        values[(signalIndex * 4) + 3] = 1_340;

        source.AppendBatch(new ColumnMajorEventBatch(4, values, layout));

        var snapshot = source.GetSnapshot(signalIndex);

        Assert.Equal(layout.SignalCount, source.SignalCount);
        Assert.Equal(1_337, snapshot.Values[snapshot.PhysicalIndexForSequence(0)]);
        Assert.Equal(1_338, snapshot.Values[snapshot.PhysicalIndexForSequence(1)]);
        Assert.Equal(1_339, snapshot.Values[snapshot.PhysicalIndexForSequence(2)]);
        Assert.Equal(1_340, snapshot.Values[snapshot.PhysicalIndexForSequence(3)]);
    }

    [Fact]
    public void AppendColumnMajorBatchRetainsOnlyRollingWindowWhenCapacityIsExceeded()
    {
        var source = new DataSource(windowCapacity: 3);
        var values = CreateColumnMajorBatch(signalCount: SignalLayout.Default.SignalCount, count: 5);

        values[(2 * 5) + 0] = 20;
        values[(2 * 5) + 1] = 21;
        values[(2 * 5) + 2] = 22;
        values[(2 * 5) + 3] = 23;
        values[(2 * 5) + 4] = 24;

        source.AppendBatchColumnMajor(values, count: 5);

        var snapshot = source.GetSnapshot(2);

        Assert.Equal(3, source.BufferedEventCount);
        Assert.Equal(3, snapshot.Count);
        Assert.Equal(2, snapshot.StartSequence);
        Assert.Equal(5, snapshot.EndSequence);
        Assert.Equal(22, snapshot.Values[snapshot.PhysicalIndexForSequence(2)]);
        Assert.Equal(23, snapshot.Values[snapshot.PhysicalIndexForSequence(3)]);
        Assert.Equal(24, snapshot.Values[snapshot.PhysicalIndexForSequence(4)]);
    }

    [Fact]
    public void ChasmDataSourceResizeWindowAppliesToNextAppendImmediately()
    {
        var source = new DataSource(windowCapacity: 5);
        var chasmSource = new ChasmDataSource(source);
        var first = CreateBatch(count: 5);
        var second = CreateBatch(count: 2);

        for (int e = 0; e < 5; e++)
            first[2][e] = 20 + e;
        second[2][0] = 25;
        second[2][1] = 26;

        chasmSource.Append(new EventBatch(5, first));
        chasmSource.SetWindowCapacity(3);
        chasmSource.Append(new EventBatch(2, second));

        var snapshot = source.GetSnapshot(2);

        Assert.Equal(3, chasmSource.WindowCapacity);
        Assert.Equal(3, source.BufferedEventCount);
        Assert.Equal(4, snapshot.StartSequence);
        Assert.Equal(7, snapshot.EndSequence);
        Assert.Equal(24, snapshot.Values[snapshot.PhysicalIndexForSequence(4)]);
        Assert.Equal(25, snapshot.Values[snapshot.PhysicalIndexForSequence(5)]);
        Assert.Equal(26, snapshot.Values[snapshot.PhysicalIndexForSequence(6)]);
    }

    [Fact]
    public void SnapshotRejectsOutOfRangeSequenceAccess()
    {
        var source = new DataSource(windowCapacity: 3);
        var batch = CreateBatch(count: 3);
        source.AppendBatch(batch, count: 3);

        var snapshot = source.GetSnapshot(0);

        Assert.Throws<ArgumentOutOfRangeException>(() => snapshot.PhysicalIndexForSequence(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => snapshot.PhysicalIndexForSequence(3));
    }

    [Fact]
    public void MultiChannelSnapshotRejectsOutOfRangeChannelAndSequenceAccess()
    {
        var source = new DataSource(windowCapacity: 3);
        var batch = CreateBatch(count: 3);
        source.AppendBatch(batch, count: 3);

        var snapshot = source.GetSnapshot(0, 1);

        Assert.Throws<ArgumentOutOfRangeException>(() => snapshot.ValueAt(2, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => snapshot.Channel(2));
        Assert.Throws<ArgumentOutOfRangeException>(() => snapshot.PhysicalIndexForSequence(3));
    }

    [Fact]
    public void SnapshotCopyIsContiguousAndStableAfterLaterAppend()
    {
        var source = new DataSource(windowCapacity: 3);
        var first = CreateBatch(count: 5);
        var second = CreateBatch(count: 1);

        for (int e = 0; e < 5; e++)
            first[2][e] = 20 + e;
        second[2][0] = 99;

        source.AppendBatch(first, count: 5);
        var snapshot = source.GetSnapshotCopy(2);
        source.AppendBatch(second, count: 1);

        Assert.True(snapshot.IsContiguous);
        Assert.Equal(0, snapshot.StartIndex);
        Assert.Equal(3, snapshot.Count);
        Assert.Equal(3, snapshot.Capacity);
        Assert.Equal(2, snapshot.StartSequence);
        Assert.Equal(5, snapshot.EndSequence);
        Assert.Equal([22, 23, 24], snapshot.Values);
    }

    [Fact]
    public void MultiChannelSnapshotCopyIsContiguousAndStableAfterLaterAppend()
    {
        var source = new DataSource(windowCapacity: 3);
        var first = CreateBatch(count: 5);
        var second = CreateBatch(count: 1);

        for (int e = 0; e < 5; e++)
        {
            first[2][e] = 20 + e;
            first[3][e] = 30 + e;
        }
        second[2][0] = 99;
        second[3][0] = 199;

        source.AppendBatch(first, count: 5);
        var snapshot = source.GetSnapshotCopy(2, 3);
        source.AppendBatch(second, count: 1);

        Assert.True(snapshot.IsContiguous);
        Assert.Equal(0, snapshot.StartIndex);
        Assert.Equal(3, snapshot.Count);
        Assert.Equal(3, snapshot.Capacity);
        Assert.Equal([22, 23, 24], snapshot.ChannelValues[0]);
        Assert.Equal([32, 33, 34], snapshot.ChannelValues[1]);
    }

    [Fact]
    public void EventBatchAcceptsCustomSignalLayout()
    {
        var layout = new SignalLayout(6, 9, 60);
        var channels = CreateBatch(count: 2, signalCount: layout.SignalCount);

        var batch = new EventBatch(2, channels, layout);

        Assert.Equal(2, batch.Count);
        Assert.Equal(3_240, batch.SignalCount);
    }

    private static double[][] CreateBatch(int count, int signalCount = SignalLayout.DefaultChannelCount)
    {
        var channels = new double[signalCount][];
        for (int i = 0; i < channels.Length; i++)
            channels[i] = new double[count];

        return channels;
    }

    private static double[] CreateColumnMajorBatch(int signalCount, int count)
    {
        return new double[signalCount * count];
    }
}
