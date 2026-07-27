using System.IO;
using System.Windows;
using System.Windows.Threading;
using SamplerRecorder.Services;

namespace SamplerRecorder;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        FileLogger.Log("=== Application starting ===");

        // Catch all unhandled exceptions
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        try
        {
            FileLogger.Log("Creating MainWindow...");
            var window = new MainWindow();
            FileLogger.Log("MainWindow created successfully. Showing...");
            window.Show();
            FileLogger.Log("MainWindow shown.");
        }
        catch (Exception ex)
        {
            FileLogger.LogException("App.OnStartup", ex);
            MessageBox.Show(
                $"SamplerRecorder failed to start:\n\n{ex.Message}\n\nSee log: %APPDATA%\\SamplerRecorder\\log.txt",
                "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        FileLogger.LogException("DispatcherUnhandledException", e.Exception);
        e.Handled = true;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            FileLogger.LogException("AppDomain.UnhandledException", ex);
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        FileLogger.LogException("UnobservedTaskException", e.Exception);
        e.SetObserved();
    }
}

