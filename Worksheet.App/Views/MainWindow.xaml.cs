using System.Windows;
using Worksheet.Core.Models;
using System;
using System.Windows.Threading;

using Worksheet.Processing;
namespace Worksheet.App.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly DispatcherTimer _gateStatsTimer = new(DispatcherPriority.Background);

        public MainWindow()
        {
            InitializeComponent();
            ToolbarControl.HistogramPlotButtonClicked += Toolbar_HistogramPlotButtonClicked;
            ToolbarControl.LoadConfigButtonClicked += Toolbar_LoadConfigButtonClicked;
            ToolbarControl.ClearMemoryButtonClicked += Toolbar_ClearMemoryButtonClicked;
            ToolbarControl.PseudocolorPlotButtonClicked += Toolbar_PseudocolorPlotButtonClicked;
            ToolbarControl.SpectralRibbonPlotButtonClicked += Toolbar_SpectralRibbonPlotButtonClicked;
            ToolbarControl.OscilloscopePlotButtonClicked += Toolbar_OscilloscopePlotButtonClicked;
            ToolbarControl.SnapGridChanged += Toolbar_SnapGridChanged;
            SidebarControl.StartStreamingClicked += Sidebar_StartStreamingClicked;
            SidebarControl.StopStreamingClicked += Sidebar_StopStreamingClicked;
            SidebarControl.RollingWindowChanged += Sidebar_RollingWindowChanged;
            ToolbarControl.SetSnapGridSize(WorksheetGridControl.SnapSize);
            SidebarControl.SetStreamingState(WorksheetGridControl.IsStreamingEnabled);
            SidebarControl.SetRollingWindowValue(WorksheetGridControl.WindowCapacity);

            _gateStatsTimer.Interval = TimeSpan.FromMilliseconds(250);
            _gateStatsTimer.Tick += (_, __) =>
            {
                try
                {
                    SidebarControl.SetGateStatsRows(WorksheetGridControl.GetGateStatsRows());
                    SidebarControl.SetProcessingStatus(WorksheetGridControl.GetProcessingStatusSnapshot());
                }
                catch
                {
                }
            };
            _gateStatsTimer.Start();
            SidebarControl.SetProcessingStatus(WorksheetGridControl.GetProcessingStatusSnapshot());

            Closed += (_, __) => _gateStatsTimer.Stop();
        }

        private void Toolbar_HistogramPlotButtonClicked(object? sender, System.EventArgs e)
        {
            WorksheetGridControl.AddPlot(PlotType.Histogram);
        }

        private void Toolbar_LoadConfigButtonClicked(object? sender, System.EventArgs e)
        {
            WorksheetGridControl.LoadConfig();
        }

        private void Toolbar_ClearMemoryButtonClicked(object? sender, System.EventArgs e)
        {
            WorksheetGridControl.ClearMemory();
        }

        private void Toolbar_PseudocolorPlotButtonClicked(object? sender, System.EventArgs e)
        {
            WorksheetGridControl.AddPlot(PlotType.Pseudocolor);
        }

        private void Toolbar_SpectralRibbonPlotButtonClicked(object? sender, System.EventArgs e)
        {
            WorksheetGridControl.AddPlot(PlotType.SpectralRibbon);
        }

        private void Toolbar_OscilloscopePlotButtonClicked(object? sender, System.EventArgs e)
        {
            WorksheetGridControl.AddPlot(PlotType.Oscilloscope);
        }

        private void Toolbar_SnapGridChanged(double snapSize)
        {
            WorksheetGridControl.SnapSize = snapSize;
        }

        private void Sidebar_StartStreamingClicked(object? sender, System.EventArgs e)
        {
            WorksheetGridControl.SetStreamingEnabled(true);
            SidebarControl.SetStreamingState(WorksheetGridControl.IsStreamingEnabled);
        }

        private void Sidebar_StopStreamingClicked(object? sender, System.EventArgs e)
        {
            WorksheetGridControl.SetStreamingEnabled(false);
            SidebarControl.SetStreamingState(WorksheetGridControl.IsStreamingEnabled);
        }

        private void Sidebar_RollingWindowChanged(int windowCapacity)
        {
            WorksheetGridControl.SetWindowCapacity(windowCapacity);
            SidebarControl.SetRollingWindowValue(WorksheetGridControl.WindowCapacity);
        }
    }
}
