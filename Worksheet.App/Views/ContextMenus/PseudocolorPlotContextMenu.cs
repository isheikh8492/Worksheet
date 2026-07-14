using Worksheet.Core.Models;
using Worksheet.Core.Services;
using Worksheet.Processing;
using Worksheet.App.Views.PlotViews;
using Worksheet.App.Views.Dialogs;

using Worksheet.App.Models;
namespace Worksheet.App.Views.ContextMenus
{
    public class PseudocolorPlotContextMenu : PlotContextMenu
    {
        private readonly FeatureSelectionStrategy _featureSelectionStrategy;

        public PseudocolorPlotContextMenu(FeatureSelectionStrategy featureSelectionStrategy)
        {
            _featureSelectionStrategy = featureSelectionStrategy;
        }

        public override void Attach(PlotItem plotItem, PlotView plotView)
        {
            if (plotView is not PseudocolorPlotView pseudocolorView)
                return;

            pseudocolorView.AttachGateInteractions(plotItem);

            var contextMenu = new System.Windows.Controls.ContextMenu();

            var addGateItem = new System.Windows.Controls.MenuItem
            {
                Header = "Add Gate"
            };

            var rectangleGateItem = new System.Windows.Controls.MenuItem
            {
                Header = "Rectangle"
            };
            rectangleGateItem.Click += (s, e) => pseudocolorView.BeginAddGateRectangle(plotItem);

            var ellipseGateItem = new System.Windows.Controls.MenuItem
            {
                Header = "Ellipse"
            };
            ellipseGateItem.Click += (s, e) => pseudocolorView.BeginAddGateEllipse(plotItem);

            var polygonGateItem = new System.Windows.Controls.MenuItem
            {
                Header = "Polygon"
            };
            polygonGateItem.Click += (s, e) => pseudocolorView.BeginAddGatePolygon(plotItem);

            addGateItem.Items.Add(rectangleGateItem);
            addGateItem.Items.Add(ellipseGateItem);
            addGateItem.Items.Add(polygonGateItem);

            var removeSelectedGateItem = new System.Windows.Controls.MenuItem
            {
                Header = "Remove Selected Gate",
                IsEnabled = false
            };
            removeSelectedGateItem.Click += (s, e) => pseudocolorView.RemoveSelectedGate(plotItem);

            var propertiesItem = new System.Windows.Controls.MenuItem
            {
                Header = "Properties"
            };
            propertiesItem.Click += (s, e) => OpenProperties(plotItem, pseudocolorView);

            var closeItem = new System.Windows.Controls.MenuItem
            {
                Header = "Close"
            };
            closeItem.Click += (s, e) => plotItem.OnCloseRequested?.Invoke(plotItem);

            contextMenu.Opened += (s, e) =>
            {
                removeSelectedGateItem.IsEnabled = pseudocolorView.HasSelectedGate();
            };

            contextMenu.Items.Add(addGateItem);
            contextMenu.Items.Add(removeSelectedGateItem);
            contextMenu.Items.Add(propertiesItem);
            contextMenu.Items.Add(closeItem);

            plotItem.PlotContainer.DragLayer.ContextMenu = contextMenu;
        }

        private void OpenProperties(PlotItem plotItem, PseudocolorPlotView pseudocolorView)
        {
            var owner = System.Windows.Window.GetWindow(plotItem.Container);
            var featureNames = _featureSelectionStrategy.GetXFeatureNames(PlotType.Pseudocolor);
            var dialog = new PseudocolorPropertiesDialog(
                pseudocolorView.Settings.XAxisScaleType,
                pseudocolorView.Settings.YAxisScaleType,
                featureNames,
                pseudocolorView.Settings.XFeature,
                pseudocolorView.Settings.YFeature)
            {
                Owner = owner
            };

            if (dialog.ShowDialog() == true)
            {
                pseudocolorView.Settings.XAxisScaleType = dialog.SelectedXAxisScale;
                pseudocolorView.Settings.YAxisScaleType = dialog.SelectedYAxisScale;
                pseudocolorView.Settings.XFeature = dialog.SelectedXFeatureIndex;
                pseudocolorView.Settings.YFeature = dialog.SelectedYFeatureIndex;
                pseudocolorView.InvalidateStatic(plotItem.Plot);
            }
        }
    }
}
