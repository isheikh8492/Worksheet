using Worksheet.Core.Models;
using Worksheet.App.Views.PlotViews;
using Worksheet.App.Views.Dialogs;

using Worksheet.App.Models;
namespace Worksheet.App.Views.ContextMenus
{
    public class SpectralRibbonPlotContextMenu : PlotContextMenu
    {
        public override void Attach(PlotItem plotItem, PlotView plotView)
        {
            if (plotView is not SpectralRibbonPlotView spectralRibbonView)
                return;

            var contextMenu = new System.Windows.Controls.ContextMenu();

            var propertiesItem = new System.Windows.Controls.MenuItem
            {
                Header = "Properties"
            };
            propertiesItem.Click += (s, e) => OpenProperties(plotItem, spectralRibbonView);

            var closeItem = new System.Windows.Controls.MenuItem
            {
                Header = "Close"
            };
            closeItem.Click += (s, e) => plotItem.OnCloseRequested?.Invoke(plotItem);

            contextMenu.Items.Add(propertiesItem);
            contextMenu.Items.Add(closeItem);

            plotItem.PlotContainer.DragLayer.ContextMenu = contextMenu;
        }

        private static void OpenProperties(PlotItem plotItem, SpectralRibbonPlotView spectralRibbonView)
        {
            var owner = System.Windows.Window.GetWindow(plotItem.Container);
            var dialog = new SpectralRibbonPropertiesDialog(spectralRibbonView.Settings.YAxisScaleType)
            {
                Owner = owner
            };

            if (dialog.ShowDialog() == true)
            {
                spectralRibbonView.Settings.YAxisScaleType = dialog.SelectedYAxisScale;
                spectralRibbonView.InvalidateStatic(plotItem.Plot);
            }
        }
    }
}
