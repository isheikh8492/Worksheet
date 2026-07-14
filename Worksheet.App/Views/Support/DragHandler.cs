using System;
using System.Windows.Controls;
using System.Windows.Input;
using Worksheet.Core.Models;

using Worksheet.App.Models;
namespace Worksheet.App.Views.Support
{
    public class DragHandler
    {
        public void AttachDrag(Border dragLayer, IWorksheetItem item, Canvas worksheet,
                               SelectionManager<IWorksheetItem> selectionManager, Func<double>? getSnapSize = null)
        {
            double dragOffsetX = 0, dragOffsetY = 0;

            dragLayer.MouseLeftButtonDown += (s, e) =>
            {
                // Select this item (handles overlapping items - topmost receives event)
                selectionManager.Select(item);

                dragOffsetX = e.GetPosition(worksheet).X - Canvas.GetLeft(item.Container);
                dragOffsetY = e.GetPosition(worksheet).Y - Canvas.GetTop(item.Container);
                dragLayer.CaptureMouse();
                e.Handled = true;
            };

            dragLayer.MouseRightButtonDown += (s, e) =>
            {
                // Right-click should also select the item before showing context menu
                selectionManager.Select(item);
            };

            dragLayer.MouseMove += (s, e) =>
            {
                if (dragLayer.IsMouseCaptured && e.LeftButton == MouseButtonState.Pressed)
                {
                    var pos = e.GetPosition(worksheet);
                    double x = pos.X - dragOffsetX;
                    double y = pos.Y - dragOffsetY;
                    double snapSize = getSnapSize?.Invoke() ?? 0;

                    // Snap to grid if enabled
                    if (snapSize > 0)
                    {
                        x = GridSnap.Snap(x, snapSize);
                        y = GridSnap.Snap(y, snapSize);
                    }

                    Canvas.SetLeft(item.Container, x);
                    Canvas.SetTop(item.Container, y);
                    e.Handled = true;
                }
            };

            dragLayer.MouseLeftButtonUp += (s, e) =>
            {
                double snapSize = getSnapSize?.Invoke() ?? 0;

                // Snap on release as well (in case snapping wasn't applied during move)
                if (snapSize > 0)
                {
                    double x = GridSnap.Snap(Canvas.GetLeft(item.Container), snapSize);
                    double y = GridSnap.Snap(Canvas.GetTop(item.Container), snapSize);
                    Canvas.SetLeft(item.Container, x);
                    Canvas.SetTop(item.Container, y);
                }

                dragLayer.ReleaseMouseCapture();
                e.Handled = true;
            };
        }

    }
}
