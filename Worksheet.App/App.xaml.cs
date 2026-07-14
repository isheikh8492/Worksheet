using System;
using System.Configuration;
using System.Data;
using System.IO;
using System.Windows;
using Worksheet.Core.Services;
using Worksheet.Processing;
using System.Threading.Tasks;

namespace Worksheet.App
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            AppLog.Initialize();
            InstallGlobalExceptionHandlers();

            // Load channel settings from JSON file
            LoadChannelConfiguration();
        }

        private void InstallGlobalExceptionHandlers()
        {
            // UI thread exceptions (WPF dispatcher)
            DispatcherUnhandledException += (_, exArgs) =>
            {
                AppLog.Exception(exArgs.Exception, "DispatcherUnhandledException");
                exArgs.Handled = true; // prevent crash
            };

            // Non-UI thread exceptions
            AppDomain.CurrentDomain.UnhandledException += (_, exArgs) =>
            {
                if (exArgs.ExceptionObject is Exception ex)
                    AppLog.Exception(ex, $"AppDomain.UnhandledException (IsTerminating={exArgs.IsTerminating})");
                else
                    AppLog.Error($"AppDomain.UnhandledException (IsTerminating={exArgs.IsTerminating})", exArgs.ExceptionObject?.ToString());
            };

            // Task exceptions
            TaskScheduler.UnobservedTaskException += (_, exArgs) =>
            {
                AppLog.Exception(exArgs.Exception, "TaskScheduler.UnobservedTaskException");
                exArgs.SetObserved();
            };
        }

        private void LoadChannelConfiguration()
        {
            // Load from application base directory (channels.json is copied to output by .csproj)
            var path = Path.Combine(AppContext.BaseDirectory, "channels.json");

            if (!File.Exists(path))
            {
                Console.WriteLine($"Warning: Channel configuration file not found at: {path}");
                Console.WriteLine("Using empty channel list.");
                return;
            }

            try
            {
                FeatureSelectionStrategy.LoadChannelSettings(path);
                var channelCount = FeatureSelectionStrategy.ChannelNames.Count;
                var allChannelCount = FeatureSelectionStrategy.AllChannelNames.Count;
                Console.WriteLine($"Loaded channel configuration from: {path}");
                Console.WriteLine($"Total channels: {allChannelCount}, Filtered (numeric) channels: {channelCount}");
                AppLog.Info("Loaded channel configuration", $"path={path} total={allChannelCount} filtered={channelCount}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load channel configuration from {path}: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                AppLog.Exception(ex, $"Failed to load channel configuration path={path}");
            }
        }
    }

}
