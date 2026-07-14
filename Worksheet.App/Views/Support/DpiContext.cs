using System;
using System.Windows;
using System.Windows.Media;

namespace Worksheet.App.Views.Support
{
    public readonly record struct DpiContext(double ScaleX, double ScaleY)
    {
        public static DpiContext Identity { get; } = new(1, 1);

        public static DpiContext From(Visual visual)
        {
            if (visual == null)
                throw new ArgumentNullException(nameof(visual));

            var dpi = VisualTreeHelper.GetDpi(visual);
            return new DpiContext(dpi.DpiScaleX, dpi.DpiScaleY);
        }

        public Rect PixelsToDips(Rect pixelRect)
        {
            return new Rect(
                pixelRect.Left / ScaleX,
                pixelRect.Top / ScaleY,
                pixelRect.Width / ScaleX,
                pixelRect.Height / ScaleY);
        }

        public int DipWidthToPixels(double width)
        {
            return Math.Max(1, (int)Math.Ceiling(width * ScaleX));
        }

        public int DipHeightToPixels(double height)
        {
            return Math.Max(1, (int)Math.Ceiling(height * ScaleY));
        }

        public Rect SnapToDevicePixels(Rect dipRect)
        {
            double left = Math.Round(dipRect.Left * ScaleX) / ScaleX;
            double top = Math.Round(dipRect.Top * ScaleY) / ScaleY;
            double right = Math.Round(dipRect.Right * ScaleX) / ScaleX;
            double bottom = Math.Round(dipRect.Bottom * ScaleY) / ScaleY;
            return new Rect(new Point(left, top), new Point(right, bottom));
        }
    }
}
