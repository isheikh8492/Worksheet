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
using System.Collections.ObjectModel;
using Worksheet.Core.Models.Gates;
using Worksheet.Core.Services;
using Worksheet.Processing;

namespace Worksheet.App.Views
{
    /// <summary>
    /// Interaction logic for Sidebar.xaml
    /// </summary>
    public partial class Sidebar : UserControl
    {
        public event EventHandler? StartStreamingClicked;
        public event EventHandler? StopStreamingClicked;
        public event Action<int>? RollingWindowChanged;
        private int _currentRollingWindowValue = 200_000;

        public ObservableCollection<GateStatsDisplayRow> GateStatsRows { get; } = new();

        public Sidebar()
        {
            InitializeComponent();
            DataContext = this;
        }

        private void StartStreamingButton_Click(object sender, RoutedEventArgs e)
        {
            StartStreamingClicked?.Invoke(this, EventArgs.Empty);
        }

        private void StopStreamingButton_Click(object sender, RoutedEventArgs e)
        {
            StopStreamingClicked?.Invoke(this, EventArgs.Empty);
        }

        private void ApplyRollingWindowButton_Click(object sender, RoutedEventArgs e)
        {
            CommitRollingWindowText();
        }

        private void RollingWindowTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;

            CommitRollingWindowText();
            e.Handled = true;
        }

        public void SetStreamingState(bool isStreamingEnabled)
        {
            StartStreamingButton.IsEnabled = !isStreamingEnabled;
            StopStreamingButton.IsEnabled = isStreamingEnabled;
            StreamingStatusText.Text = isStreamingEnabled ? "Status: Running" : "Status: Stopped";
            StreamingStatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isStreamingEnabled ? "#166534" : "#991B1B"));
        }

        public void SetRollingWindowValue(int value)
        {
            _currentRollingWindowValue = value;
            RollingWindowTextBox.Text = value.ToString();
        }

        public void SetGateStatsRows(IEnumerable<GateStatsDisplayRow> rows)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => SetGateStatsRows(rows));
                return;
            }

            GateStatsRows.Clear();
            foreach (var row in rows)
                GateStatsRows.Add(row);
        }

        public void SetProcessingStatus(ProcessingStatusSnapshot status)
        {
            EventRateValueText.Text = $"{status.EventRatePerSecond:F0} ev/s";
            BufferedEventsValueText.Text = status.BufferedEventCount.ToString("N0");
            HistogramComputeValueText.Text = FormatMilliseconds(status.HistogramAverageComputeMs);
            PseudocolorComputeValueText.Text = FormatMilliseconds(status.PseudocolorAverageComputeMs);
            SpectralComputeValueText.Text = FormatMilliseconds(status.SpectralRibbonAverageComputeMs);
            OscilloscopeComputeValueText.Text = FormatMilliseconds(status.OscilloscopeAverageComputeMs);
            HistogramRenderValueText.Text = FormatMilliseconds(status.HistogramAverageRenderMs);
            PseudocolorRenderValueText.Text = FormatMilliseconds(status.PseudocolorAverageRenderMs);
            SpectralRenderValueText.Text = FormatMilliseconds(status.SpectralRibbonAverageRenderMs);
            OscilloscopeRenderValueText.Text = FormatMilliseconds(status.OscilloscopeAverageRenderMs);
        }

        private static string FormatMilliseconds(double value)
        {
            return value > 0 ? $"{value:F2} ms" : "--";
        }

        private void CommitRollingWindowText()
        {
            if (!int.TryParse(RollingWindowTextBox.Text, out int value) || value <= 0)
            {
                RollingWindowTextBox.Text = _currentRollingWindowValue.ToString();
                return;
            }

            _currentRollingWindowValue = value;
            RollingWindowTextBox.Text = value.ToString();
            RollingWindowChanged?.Invoke(value);
        }
    }
}
