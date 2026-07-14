using System;

namespace Worksheet.Processing
{
    public sealed class HeatmapColorPalette
    {
        private readonly byte[] _bgra;

        private HeatmapColorPalette(byte[] bgra)
        {
            _bgra = bgra;
        }

        public static HeatmapColorPalette MellowRainbow { get; } = CreateMellowRainbow();

        public void WriteNormalizedPixel(double normalized, byte[] pixelBuffer, int pixelIndex)
        {
            if (double.IsNaN(normalized) || normalized <= 0)
            {
                pixelBuffer[pixelIndex + 0] = 255;
                pixelBuffer[pixelIndex + 1] = 255;
                pixelBuffer[pixelIndex + 2] = 255;
                pixelBuffer[pixelIndex + 3] = 0;
                return;
            }

            int paletteIndex = Math.Clamp((int)Math.Round(normalized * 255), 0, 255);
            int paletteOffset = paletteIndex * 4;
            pixelBuffer[pixelIndex + 0] = _bgra[paletteOffset + 0];
            pixelBuffer[pixelIndex + 1] = _bgra[paletteOffset + 1];
            pixelBuffer[pixelIndex + 2] = _bgra[paletteOffset + 2];
            pixelBuffer[pixelIndex + 3] = _bgra[paletteOffset + 3];
        }

        private static HeatmapColorPalette CreateMellowRainbow()
        {
            (byte r, byte g, byte b)[] stops =
            [
                (0x00, 0x00, 0xFF),
                (0x00, 0xFF, 0xFF),
                (0x00, 0xFF, 0x00),
                (0xFF, 0xFF, 0x00),
                (0xFF, 0x00, 0x00),
            ];

            var bgra = new byte[256 * 4];
            for (int i = 0; i < 256; i++)
            {
                double position = i / 255.0;
                double scaled = position * (stops.Length - 1);
                int lower = Math.Clamp((int)Math.Floor(scaled), 0, stops.Length - 1);
                int upper = Math.Clamp(lower + 1, 0, stops.Length - 1);
                double t = scaled - lower;

                byte r = LerpByte(stops[lower].r, stops[upper].r, t);
                byte g = LerpByte(stops[lower].g, stops[upper].g, t);
                byte b = LerpByte(stops[lower].b, stops[upper].b, t);

                int offset = i * 4;
                bgra[offset + 0] = b;
                bgra[offset + 1] = g;
                bgra[offset + 2] = r;
                bgra[offset + 3] = 255;
            }

            return new HeatmapColorPalette(bgra);
        }

        private static byte LerpByte(byte start, byte end, double t)
        {
            return (byte)Math.Clamp((int)Math.Round(start + ((end - start) * t)), 0, 255);
        }
    }
}
