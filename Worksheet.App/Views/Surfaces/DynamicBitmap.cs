using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Worksheet.App.Views.Support;

namespace Worksheet.App.Views.Surfaces
{
    public sealed class DynamicBitmap : Image
    {
        private WriteableBitmap? _bitmap;
        private int _targetWidth;
        private int _targetHeight;

        public DynamicBitmap()
        {
            IsHitTestVisible = false;
            Stretch = Stretch.Fill;
            HorizontalAlignment = HorizontalAlignment.Left;
            VerticalAlignment = VerticalAlignment.Top;
            Visibility = Visibility.Collapsed;
            SnapsToDevicePixels = true;
            UseLayoutRounding = true;
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
            RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);
        }

        public Rect DataRect { get; private set; }
        public int TargetWidth => Volatile.Read(ref _targetWidth);
        public int TargetHeight => Volatile.Read(ref _targetHeight);
        public DpiContext DpiContext { get; private set; } = DpiContext.Identity;

        public void SetDataRect(Rect plotDataRectPixels)
        {
            SetDataRect(plotDataRectPixels, DpiContext.Identity);
        }

        public void SetDataRect(Rect plotDataRectDip, DpiContext dpi)
        {
            DataRect = plotDataRectDip;
            DpiContext = dpi;
            Width = Math.Max(0, plotDataRectDip.Width);
            Height = Math.Max(0, plotDataRectDip.Height);
            Margin = new Thickness(plotDataRectDip.X, plotDataRectDip.Y, 0, 0);

            Volatile.Write(ref _targetWidth, dpi.DipWidthToPixels(plotDataRectDip.Width));
            Volatile.Write(ref _targetHeight, dpi.DipHeightToPixels(plotDataRectDip.Height));
        }

        public void PresentBitmap(byte[] buffer, int width, int height)
        {
            if (buffer == null || buffer.Length == 0 || width <= 0 || height <= 0)
            {
                Clear();
                return;
            }

            const int bytesPerPixel = 4;
            int stride = width * bytesPerPixel;
            if (_bitmap == null || _bitmap.PixelWidth != width || _bitmap.PixelHeight != height)
            {
                _bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
                Source = _bitmap;
            }

            _bitmap.WritePixels(new Int32Rect(0, 0, width, height), buffer, stride, 0);
            Visibility = Visibility.Visible;
        }

        public void Clear()
        {
            Source = null;
            _bitmap = null;
            Visibility = Visibility.Collapsed;
        }
    }
}
