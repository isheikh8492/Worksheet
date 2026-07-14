namespace Worksheet.Core.Models
{
    public readonly record struct RenderTargetSize(int PixelWidth, int PixelHeight)
    {
        public static RenderTargetSize Empty { get; } = new(0, 0);

        public bool HasPixels => PixelWidth > 0 && PixelHeight > 0;
    }
}
