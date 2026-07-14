using System;
using Worksheet.Core.Models;

namespace Worksheet.Chasm
{
    public enum ProducerThroughputMode
    {
        FixedRate,
        MaxThroughput
    }

    public enum ChasmPreset
    {
        Safe25k,
        Balanced50k,
        Stress100k,
        MaxThroughput
    }

    public sealed record ChasmOptions(
        TimeSpan AcquisitionInterval,
        int BatchSize,
        int ChannelCapacityBatches,
        int WindowCapacityEvents,
        SignalLayout SignalLayout,
        int Seed,
        ProducerThroughputMode ThroughputMode)
    {
        public static ChasmOptions Default =>
            new(
                TimeSpan.FromMilliseconds(25),
                BatchSize: 500,
                ChannelCapacityBatches: 8,
                WindowCapacityEvents: 200_000,
                SignalLayout: SignalLayout.Default,
                Seed: 12345,
                ThroughputMode: ProducerThroughputMode.FixedRate);

        public static ChasmOptions Safe25k => Default;

        public static ChasmOptions Balanced50k =>
            Default with
            {
                AcquisitionInterval = TimeSpan.FromMilliseconds(10),
                BatchSize = 500,
                ThroughputMode = ProducerThroughputMode.FixedRate
            };

        public static ChasmOptions Stress100k =>
            Default with
            {
                AcquisitionInterval = TimeSpan.FromMilliseconds(5),
                BatchSize = 500,
                ThroughputMode = ProducerThroughputMode.FixedRate
            };

        public static ChasmOptions MaxThroughput =>
            Default with { ThroughputMode = ProducerThroughputMode.MaxThroughput };

        public static ChasmOptions FiftyThousandEventsPerSecond =>
            Balanced50k;

        public static ChasmOptions FromPreset(ChasmPreset preset)
        {
            return preset switch
            {
                ChasmPreset.Safe25k => Safe25k,
                ChasmPreset.Balanced50k => Balanced50k,
                ChasmPreset.Stress100k => Stress100k,
                ChasmPreset.MaxThroughput => MaxThroughput,
                _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "Unsupported CHASM preset.")
            };
        }
    }
}
