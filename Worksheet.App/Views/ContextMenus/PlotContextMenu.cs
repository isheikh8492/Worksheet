using Worksheet.Core.Models;
using Worksheet.App.Views.PlotViews;

using Worksheet.App.Models;
namespace Worksheet.App.Views.ContextMenus
{
    public abstract class PlotContextMenu
    {
        public abstract void Attach(PlotItem plotItem, PlotView plotView);
    }
}
