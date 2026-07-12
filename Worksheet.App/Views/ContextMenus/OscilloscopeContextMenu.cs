using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Worksheet.Models;
using Worksheet.Services;
using Worksheet.Views.PlotViews;
using Worksheet.Views.PlotViews.Dialogs;

namespace Worksheet.Views.PlotViews.ContextMenus
{
    public class OscilloscopeContextMenu : PlotContextMenu
    {
        public override void Attach(PlotItem plotItem, PlotView plotView)
        {
            if (plotView is not OscilloscopePlotView oscilloscopeView)
                return;

            var contextMenu = new ContextMenu();

            var propertiesItem = new MenuItem
            {
                Header = "Properties"
            };
            propertiesItem.Click += (s, e) => OpenProperties(plotItem, oscilloscopeView);

            var closeItem = new MenuItem
            {
                Header = "Close"
            };
            closeItem.Click += (s, e) => plotItem.OnCloseRequested?.Invoke(plotItem);

            contextMenu.Items.Add(propertiesItem);
            contextMenu.Items.Add(closeItem);

            plotItem.PlotContainer.DragLayer.ContextMenu = contextMenu;
        }

        private static void OpenProperties(PlotItem plotItem, OscilloscopePlotView oscilloscopeView)
        {
            var selected = oscilloscopeView.Settings.ChannelIndices;
            int maxSelectedIndex = selected.Length > 0 ? selected.Max() : 0;
            var configuredChannelNames = FeatureSelectionStrategy.AllChannelNames;
            int channelCount = Math.Max(
                Math.Max(oscilloscopeView.Settings.ChannelCount, configuredChannelNames.Count),
                maxSelectedIndex + 1);

            var channelLabels = BuildChannelLabels(channelCount, configuredChannelNames);
            var dialog = new OscilloscopePropertiesDialog(channelLabels, selected)
            {
                Owner = Window.GetWindow(plotItem.Container)
            };

            if (dialog.ShowDialog() == true)
            {
                oscilloscopeView.Settings.ChannelIndices = dialog.SelectedChannelIndices;
                oscilloscopeView.Settings.ChannelCount = Math.Max(channelCount, dialog.SelectedChannelIndices.Max() + 1);
                oscilloscopeView.InvalidateStatic(plotItem.Plot);
            }
        }

        private static string[] BuildChannelLabels(int channelCount, IReadOnlyList<string> configuredChannelNames)
        {
            var labels = new string[channelCount];
            for (int i = 0; i < labels.Length; i++)
            {
                string name = i < configuredChannelNames.Count ? configuredChannelNames[i] : string.Empty;
                labels[i] = string.IsNullOrWhiteSpace(name)
                    ? $"Channel {i}"
                    : $"{i}: {name}";
            }

            return labels;
        }
    }
}
