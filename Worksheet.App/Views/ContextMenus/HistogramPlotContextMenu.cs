using System.Windows.Controls;
using System.Windows;
using Worksheet.Core.Models;
using Worksheet.Core.Services;
using Worksheet.Processing;
using Worksheet.App.Views.PlotViews;
using Worksheet.App.Views.Dialogs;

using Worksheet.App.Models;
namespace Worksheet.App.Views.ContextMenus
{
    public class HistogramPlotContextMenu : PlotContextMenu
    {
        private readonly FeatureSelectionStrategy _featureSelectionStrategy;

        public HistogramPlotContextMenu(FeatureSelectionStrategy featureSelectionStrategy)
        {
            _featureSelectionStrategy = featureSelectionStrategy;
        }

        public override void Attach(PlotItem plotItem, PlotView plotView)
        {
            if (plotView is not HistogramPlotView histogramView)
                return;

            var contextMenu = new ContextMenu();
            histogramView.AttachGateInteractions(plotItem);

            var addGateItem = new MenuItem
            {
                Header = "Add Gate"
            };

            var lineGateItem = new MenuItem
            {
                Header = "Line"
            };
            lineGateItem.Click += (s, e) => histogramView.BeginAddLineGate(plotItem);
            addGateItem.Items.Add(lineGateItem);

            var removeSelectedGateItem = new MenuItem
            {
                Header = "Remove Selected Gate"
            };
            removeSelectedGateItem.Click += (s, e) =>
            {
                if (!histogramView.RemoveSelectedGate(plotItem))
                    return;

                plotItem.Plot.Refresh();
            };

            contextMenu.Opened += (s, e) =>
            {
                removeSelectedGateItem.IsEnabled = histogramView.HasSelectedGate();
            };

            var propertiesItem = new MenuItem
            {
                Header = "Properties"
            };
            propertiesItem.Click += (s, e) => OpenProperties(plotItem, histogramView);

            var closeItem = new MenuItem
            {
                Header = "Close"
            };
            closeItem.Click += (s, e) => plotItem.OnCloseRequested?.Invoke(plotItem);

            contextMenu.Items.Add(addGateItem);
            contextMenu.Items.Add(removeSelectedGateItem);
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(propertiesItem);
            contextMenu.Items.Add(closeItem);

            plotItem.PlotContainer.DragLayer.ContextMenu = contextMenu;
        }

        private void OpenProperties(PlotItem plotItem, HistogramPlotView histogramView)
        {
            var owner = Window.GetWindow(plotItem.Container);
            var channelNames = _featureSelectionStrategy.GetXFeatureNames(PlotType.Histogram);
            var dialog = new HistogramPropertiesDialog(histogramView.CurrentAxisScale, channelNames, histogramView.Settings.XFeature)
            {
                Owner = owner
            };

            if (dialog.ShowDialog() == true)
            {
                histogramView.UpdateAxisScale(plotItem, dialog.SelectedAxisScale);
                histogramView.Settings.XFeature = dialog.SelectedChannelIndex;
                histogramView.InvalidateStatic(plotItem.Plot);
            }
        }
    }
}
