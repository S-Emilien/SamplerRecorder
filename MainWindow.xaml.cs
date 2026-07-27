using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SamplerRecorder.Controls;
using SamplerRecorder.Models;
using SamplerRecorder.Services;
using SamplerRecorder.ViewModels;

namespace SamplerRecorder;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private bool _isMinimizedToTray;
    private H.NotifyIcon.TaskbarIcon? _trayIcon;

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
            _vm.SeekTo(ms);
        };

        // Redraw waveform periodically
        var redrawTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        redrawTimer.Tick += (_, _) =>
        {
            if (EditorScreen.Visibility == Visibility.Visible)
            {
                WaveformDisplay.PlaybackPositionMs = _vm.PlaybackPosition;
                WaveformDisplay.ViewStartMs = _vm.ViewStartMs;
                WaveformDisplay.ViewEndMs = _vm.ViewEndMs;

                // Don't overwrite selection while user is actively dragging it
                if (!WaveformDisplay.IsSelectingInteraction)
                {
                    WaveformDisplay.SelectionStartMs = _vm.SelectionStart;
                    WaveformDisplay.SelectionEndMs = _vm.SelectionEnd;
                }

                WaveformDisplay.Markers = new List<Marker>(_vm.Markers);
                WaveformDisplay.Redraw();
            }
        };
        redrawTimer.Start();

        // Show tray icon on startup
        ShowTrayIcon();

        // Wire up hotkey capture controls
        WireHotkeyControl(HotkeyStart);
        WireHotkeyControl(HotkeyPause);
        WireHotkeyControl(HotkeyStop);
        WireHotkeyControl(HotkeyMarker);
    }

    private void WireHotkeyControl(SamplerRecorder.Controls.HotkeyCaptureControl control)
    {
        control.HotkeyCaptured += (action, binding) => _vm.SetHotkey(action, binding);
        control.HotkeyCleared += action => _vm.ClearHotkey(action);
    }

    private void RecordingsList_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (RecordingsList.SelectedItem is SessionItemViewModel session)
        {
            _vm.OpenSessionCommand.Execute(session);
            SwitchToEditor();
        }
    }

    private void BackToHome_Click(object sender, RoutedEventArgs e)
    {
        SwitchToHome();
    }

    private void SwitchToEditor()
    {
        HomeScreen.Visibility = Visibility.Collapsed;
        EditorScreen.Visibility = Visibility.Visible;
    }

    private void SwitchToHome()
    {
        _vm.ResetEditorState();
        EditorScreen.Visibility = Visibility.Collapsed;
        SettingsScreen.Visibility = Visibility.Collapsed;
        HomeScreen.Visibility = Visibility.Visible;
        _vm.RefreshSessionsList();
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        _vm.OpenSettingsCommand.Execute(null);
        HomeScreen.Visibility = Visibility.Collapsed;
        EditorScreen.Visibility = Visibility.Collapsed;
        SettingsScreen.Visibility = Visibility.Visible;
    }

    private void BackToHome_FromSettings_Click(object sender, RoutedEventArgs e)
    {
        _vm.CloseSettingsCommand.Execute(null);
        SettingsScreen.Visibility = Visibility.Collapsed;
        HomeScreen.Visibility = Visibility.Visible;
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
        }
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_isMinimizedToTray)
        {
            _vm.Dispose();
            _trayIcon?.Dispose();
        }
        else
        {
            e.Cancel = true;
            Hide();
        }
    }

    private void ShowTrayIcon()
    {
        if (_trayIcon != null) return;

        try
        {
            _trayIcon = new H.NotifyIcon.TaskbarIcon
            {
                ToolTipText = "SamplerRecorder",
                Visibility = Visibility.Visible
            };
            _trayIcon.Icon = LoadAppIcon();

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
            FileLogger.Log("Tray icon created.");
        }
        catch (Exception ex)
        {
            FileLogger.LogException("ShowTrayIcon", ex);
        }
    }

    private static System.Drawing.Icon LoadAppIcon()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/Resources/app.ico", UriKind.Absolute);
            var streamInfo = System.Windows.Application.GetResourceStream(uri);
            if (streamInfo != null)
            {
                using var stream = streamInfo.Stream;
                return new System.Drawing.Icon(stream, 16, 16);
            }
        }
        catch { /* fall through to fallback */ }

        // Fallback: simple colored circle if icon resource is missing
        using var bmp = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(System.Drawing.Color.Transparent);
            using var brush = new SolidBrush(System.Drawing.Color.FromArgb(255, 80, 180, 255));
            g.FillEllipse(brush, 1, 1, 14, 14);
        }
        return System.Drawing.Icon.FromHandle(bmp.GetHicon());
    }

    private void RestoreFromTray()
    {
        _isMinimizedToTray = false;
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }
}
