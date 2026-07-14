using System.Windows;
using Worksheet.Models;

namespace Worksheet.Views.PlotViews.Dialogs
{
    public partial class HistogramPropertiesDialog : Window
    {
        public ScaleType SelectedAxisScale { get; private set; }
        public int SelectedChannelIndex { get; private set; }

        public HistogramPropertiesDialog(ScaleType currentScale, System.Collections.Generic.IReadOnlyList<string> channelNames, int currentChannelIndex)
        {
            InitializeComponent();

            AxisScaleComboBox.ItemsSource = new[] { ScaleType.Linear, ScaleType.Logarithmic };
            AxisScaleComboBox.SelectedItem = currentScale;

            ChannelComboBox.ItemsSource = channelNames;
            if (channelNames.Count > 0)
            {
                if (currentChannelIndex < 0)
                    currentChannelIndex = 0;
                if (currentChannelIndex >= channelNames.Count)
                    currentChannelIndex = channelNames.Count - 1;
                ChannelComboBox.SelectedIndex = currentChannelIndex;
            }
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (AxisScaleComboBox.SelectedItem is ScaleType selected && ChannelComboBox.SelectedIndex >= 0)
            {
                SelectedAxisScale = selected;
                SelectedChannelIndex = ChannelComboBox.SelectedIndex;
                DialogResult = true;
            }
            else
            {
                DialogResult = false;
            }
        }
    }
}
