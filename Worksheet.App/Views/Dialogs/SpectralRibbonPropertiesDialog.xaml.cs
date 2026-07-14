using System.Windows;
using Worksheet.Models;

namespace Worksheet.Views.PlotViews.Dialogs
{
    public partial class SpectralRibbonPropertiesDialog : Window
    {
        public ScaleType SelectedYAxisScale { get; private set; }

        public SpectralRibbonPropertiesDialog(ScaleType currentScale)
        {
            InitializeComponent();

            YAxisScaleComboBox.ItemsSource = new[] { ScaleType.Linear, ScaleType.Logarithmic };
            YAxisScaleComboBox.SelectedItem = currentScale;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (YAxisScaleComboBox.SelectedItem is ScaleType yScale)
            {
                SelectedYAxisScale = yScale;
                DialogResult = true;
            }
            else
            {
                DialogResult = false;
            }
        }
    }
}
