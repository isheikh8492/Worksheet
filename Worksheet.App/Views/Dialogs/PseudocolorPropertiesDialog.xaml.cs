using System.Collections.Generic;
using System.Windows;
using Worksheet.Models;

namespace Worksheet.Views.PlotViews.Dialogs
{
    public partial class PseudocolorPropertiesDialog : Window
    {
        public int SelectedXFeatureIndex { get; private set; }
        public int SelectedYFeatureIndex { get; private set; }
        public ScaleType SelectedXAxisScale { get; private set; }
        public ScaleType SelectedYAxisScale { get; private set; }

        public PseudocolorPropertiesDialog(
            ScaleType currentXScale,
            ScaleType currentYScale,
            IReadOnlyList<string> featureNames,
            int currentXFeatureIndex,
            int currentYFeatureIndex)
        {
            InitializeComponent();

            XAxisScaleComboBox.ItemsSource = new[] { ScaleType.Linear, ScaleType.Logarithmic };
            YAxisScaleComboBox.ItemsSource = new[] { ScaleType.Linear, ScaleType.Logarithmic };
            XAxisScaleComboBox.SelectedItem = currentXScale;
            YAxisScaleComboBox.SelectedItem = currentYScale;

            XFeatureComboBox.ItemsSource = featureNames;
            YFeatureComboBox.ItemsSource = featureNames;

            if (featureNames.Count > 0)
            {
                if (currentXFeatureIndex < 0)
                    currentXFeatureIndex = 0;
                if (currentXFeatureIndex >= featureNames.Count)
                    currentXFeatureIndex = featureNames.Count - 1;
                if (currentYFeatureIndex < 0)
                    currentYFeatureIndex = 0;
                if (currentYFeatureIndex >= featureNames.Count)
                    currentYFeatureIndex = featureNames.Count - 1;

                XFeatureComboBox.SelectedIndex = currentXFeatureIndex;
                YFeatureComboBox.SelectedIndex = currentYFeatureIndex;
            }
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (XAxisScaleComboBox.SelectedItem is ScaleType xScale &&
                YAxisScaleComboBox.SelectedItem is ScaleType yScale &&
                XFeatureComboBox.SelectedIndex >= 0 &&
                YFeatureComboBox.SelectedIndex >= 0)
            {
                SelectedXAxisScale = xScale;
                SelectedYAxisScale = yScale;
                SelectedXFeatureIndex = XFeatureComboBox.SelectedIndex;
                SelectedYFeatureIndex = YFeatureComboBox.SelectedIndex;
                DialogResult = true;
            }
            else
            {
                DialogResult = false;
            }
        }
    }
}
