using System;

namespace Worksheet.Models
{
    /// <summary>
    /// Maps a data value onto a plot's bin axis. A scale is built once for a given value range
    /// and bin count, then reused: <see cref="ToBin"/> for processing (clamped bin index) and
    /// <see cref="ToPosition"/> for axis ticks (fractional bin position).
    /// </summary>
    public abstract class Scale
    {
        /// <summary>Fractional bin position of <paramref name="value"/> (may be outside [0, bins]).</summary>
        public abstract double ToPosition(double value);

        /// <summary>Clamped integer bin index of <paramref name="value"/> in [0, bins - 1].</summary>
        public abstract int ToBin(double value);

        public static Scale Create(ScaleType type, double min, double max, int bins) => type switch
        {
            ScaleType.Linear => new LinearScale(min, max, bins),
            ScaleType.Logarithmic => new LogScale(min, max, bins),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported scale type."),
        };
    }

    /// <summary>Linear value-to-bin mapping over [min, max].</summary>
    public sealed class LinearScale : Scale
    {
        private readonly double _scale;
        private readonly double _offset;
        private readonly double _min;
        private readonly double _max;
        private readonly int _bins;

        public LinearScale(double min, double max, int bins)
        {
            if (max <= min)
                max = min + 1;

            _min = min;
            _max = max;
            _bins = bins;
            _scale = bins / (max - min);
            _offset = -min * _scale;
        }

        public override double ToPosition(double value)
        {
            if (value < _min) value = _min;
            else if (value > _max) value = _max;
            return value * _scale + _offset;
        }

        public override int ToBin(double value) => Math.Clamp((int)ToPosition(value), 0, _bins - 1);
    }

    /// <summary>Logarithmic (base-10) value-to-bin mapping over [min, max].</summary>
    public sealed class LogScale : Scale
    {
        private readonly double _scale;
        private readonly double _offset;
        private readonly double _min;
        private readonly double _max;
        private readonly int _bins;

        public LogScale(double min, double max, int bins)
        {
            if (min < 1)
                min = 1;
            if (max <= min)
                max = min * 10;

            _min = min;
            _max = max;
            _bins = bins;
            double minLog = Math.Log10(min);
            _scale = bins / (Math.Log10(max) - minLog);
            _offset = -minLog * _scale;
        }

        public override double ToPosition(double value)
        {
            if (value < _min) value = _min;
            else if (value > _max) value = _max;
            return Math.Log10(value) * _scale + _offset;
        }

        public override int ToBin(double value) => Math.Clamp((int)ToPosition(value), 0, _bins - 1);
    }
}
