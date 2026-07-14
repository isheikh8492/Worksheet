using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Worksheet.App.Views
{
    /// <summary>
    /// Interaction logic for Toolbar.xaml
    /// </summary>
    public partial class Toolbar : UserControl
    {
        public event EventHandler? HistogramPlotButtonClicked;
        public event EventHandler? LoadConfigButtonClicked;
        public event EventHandler? ClearMemoryButtonClicked;
        public event EventHandler? PseudocolorPlotButtonClicked;
        public event EventHandler? SpectralRibbonPlotButtonClicked;
        public event EventHandler? OscilloscopePlotButtonClicked;
        public event Action<double>? SnapGridChanged;
        private double _currentSnapSize = 20;

        public Toolbar()
        {
            InitializeComponent();
        }

        private void HistogramPlotButton_Click(object sender, RoutedEventArgs e)
        {
            HistogramPlotButtonClicked?.Invoke(this, EventArgs.Empty);
        }

        private void LoadConfigButton_Click(object sender, RoutedEventArgs e)
        {
            LoadConfigButtonClicked?.Invoke(this, EventArgs.Empty);
        }

        private void ClearMemoryButton_Click(object sender, RoutedEventArgs e)
        {
            ClearMemoryButtonClicked?.Invoke(this, EventArgs.Empty);
        }

        private void PseudocolorPlotButton_Click(object sender, RoutedEventArgs e)
        {
            PseudocolorPlotButtonClicked?.Invoke(this, EventArgs.Empty);
        }

        private void SpectralRibbonPlotButton_Click(object sender, RoutedEventArgs e)
        {
            SpectralRibbonPlotButtonClicked?.Invoke(this, EventArgs.Empty);
        }

        private void OscilloscopePlotButton_Click(object sender, RoutedEventArgs e)
        {
            OscilloscopePlotButtonClicked?.Invoke(this, EventArgs.Empty);
        }

        private void SnapToGridCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            CommitSnapGridSettings();
        }

        private void SnapGridSizeTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            CommitSnapGridSettings();
        }

        private void SnapGridSizeTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;

            CommitSnapGridSettings();
            e.Handled = true;
        }

        public void SetSnapGridSize(double snapSize)
        {
            _currentSnapSize = snapSize > 0 ? snapSize : _currentSnapSize;
            SnapToGridCheckBox.IsChecked = snapSize > 0;
            SnapGridSizeTextBox.Text = Math.Round(_currentSnapSize).ToString();
        }

        private void CommitSnapGridSettings()
        {
            if (!double.TryParse(SnapGridSizeTextBox.Text, out double snapSize) || snapSize <= 0)
            {
                snapSize = _currentSnapSize;
                SnapGridSizeTextBox.Text = Math.Round(snapSize).ToString();
            }
            else
            {
                _currentSnapSize = snapSize;
                SnapGridSizeTextBox.Text = Math.Round(snapSize).ToString();
            }

            SnapGridChanged?.Invoke(SnapToGridCheckBox.IsChecked == true ? snapSize : 0);
        }
    }
}
