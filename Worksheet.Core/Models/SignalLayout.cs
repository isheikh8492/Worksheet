using System;

namespace Worksheet.Core.Models
{
    public readonly record struct SignalLayout
    {
        public const int DefaultLaserCount = 1;
        public const int DefaultFeatureCount = 1;
        public const int DefaultChannelCount = 51;

        public SignalLayout(int laserCount, int featureCount, int channelCount)
        {
            if (laserCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(laserCount));
            if (featureCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(featureCount));
            if (channelCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(channelCount));

            LaserCount = laserCount;
            FeatureCount = featureCount;
            ChannelCount = channelCount;
            SignalCount = checked(laserCount * featureCount * channelCount);
        }

        public int LaserCount { get; }
        public int FeatureCount { get; }
        public int ChannelCount { get; }
        public int SignalCount { get; }

        public static SignalLayout Default { get; } = new(DefaultLaserCount, DefaultFeatureCount, DefaultChannelCount);

        public int ToIndex(int laser, int feature, int channel)
        {
            ValidateIndex(laser, LaserCount, nameof(laser));
            ValidateIndex(feature, FeatureCount, nameof(feature));
            ValidateIndex(channel, ChannelCount, nameof(channel));

            return ((laser * FeatureCount) + feature) * ChannelCount + channel;
        }

        public int ToIndex(SignalKey key) => ToIndex(key.Laser, key.Feature, key.Channel);

        private static void ValidateIndex(int value, int count, string name)
        {
            if (value < 0 || value >= count)
                throw new ArgumentOutOfRangeException(name, value, $"Index must be in [0, {count - 1}].");
        }
    }
}
