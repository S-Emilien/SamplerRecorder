using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SamplerRecorder.Controls;
using SamplerRecorder.Models;
using SamplerRecorder.ViewModels;

namespace SamplerRecorder;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private bool _isMinimizedToTray;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel();
        DataContext = _vm;

        // Wire up waveform control
        WaveformDisplay.WaveformService = _vm.WaveformService;
        WaveformDisplay.Markers = new List<Marker>(_vm.Markers);

        WaveformDisplay.ViewChanged += (start, end) =>
        {
            _vm.ViewStartMs = start;
            _vm.ViewEndMs = end;
        };

        WaveformDisplay.SelectionChanged += (start, end) =>
        {
            _vm.SelectionStart = start;
            _vm.SelectionEnd = end;
        };

        WaveformDisplay.SeekRequested += ms =>
        {
            _vm.PlaybackPosition = ms;
        };

        // Redraw waveform periodically
        var redrawTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        redrawTimer.Tick += (_, _) =>
        {
            WaveformDisplay.PlaybackPositionMs = _vm.PlaybackPosition;
            WaveformDisplay.ViewStartMs = _vm.ViewStartMs;
            WaveformDisplay.ViewEndMs = _vm.ViewEndMs;
            WaveformDisplay.SelectionStartMs = _vm.SelectionStart;
            WaveformDisplay.SelectionEndMs = _vm.SelectionEnd;
            WaveformDisplay.Markers = new List<Marker>(_vm.Markers);
            WaveformDisplay.Redraw();
        };
        redrawTimer.Start();
    }

    private void Marker_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem item && item.DataContext is Marker marker)
        {
            _vm.JumpToMarkerCommand.Execute(marker);
        }
    }

    private void Window_StateChanged(object sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized && _vm.Settings.MinimizeToTray)
        {
            Hide();
            _isMinimizedToTray = true;
            // Show tray notification
            ShowTrayIcon();
        }
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_isMinimizedToTray)
        {
            _vm.Dispose();
        }
        else
        {
            e.Cancel = true;
            Hide();
        }
    }

    private H.NotifyIcon.TaskbarIcon? _trayIcon;

    private void ShowTrayIcon()
    {
        if (_trayIcon != null) return;

        _trayIcon = new H.NotifyIcon.TaskbarIcon
        {
            ToolTipText = "SamplerRecorder - Recording",
            Icon = System.Drawing.SystemIcons.Application
        };

        var contextMenu = new ContextMenu();
        var showItem = new MenuItem { Header = "Show" };
        showItem.Click += (_, _) => RestoreFromTray();
        var exitItem = new MenuItem { Header = "Exit" };
        exitItem.Click += (_, _) =>
        {
            _vm.Dispose();
            _trayIcon?.Dispose();
            Application.Current.Shutdown();
        };
        contextMenu.Items.Add(showItem);
        contextMenu.Items.Add(exitItem);
        _trayIcon.ContextMenu = contextMenu;

        _trayIcon.TrayLeftMouseDown += (_, _) => RestoreFromTray();
    }

    private void RestoreFromTray()
    {
        _isMinimizedToTray = false;
        Show();
        WindowState = WindowState.Normal;
        Activate();
        _trayIcon?.Dispose();
        _trayIcon = null;
    }
}
