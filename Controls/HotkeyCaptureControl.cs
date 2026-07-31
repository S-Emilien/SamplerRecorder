using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SamplerRecorder.Models;

namespace SamplerRecorder.Controls;

/// <summary>
/// A control that displays a hotkey binding and allows the user to capture a new one.
/// </summary>
public sealed class HotkeyCaptureControl : UserControl
{
    /// <summary>Tracks the single control currently in capture mode (mutual exclusion).</summary>
    private static HotkeyCaptureControl? _activeCapture;

    private readonly TextBlock _displayText;
    private readonly Button _assignButton;
    private readonly Button _clearButton;
    private bool _isCapturing;

    public static readonly DependencyProperty ActionNameProperty =
        DependencyProperty.Register(nameof(ActionName), typeof(string), typeof(HotkeyCaptureControl),
            new PropertyMetadata("Action"));

    public static readonly DependencyProperty BindingDisplayProperty =
        DependencyProperty.Register(nameof(BindingDisplay), typeof(string), typeof(HotkeyCaptureControl),
            new PropertyMetadata("Not assigned", OnBindingDisplayChanged));

    public string ActionName
    {
        get => (string)GetValue(ActionNameProperty);
        set => SetValue(ActionNameProperty, value);
    }

    public string BindingDisplay
    {
        get => (string)GetValue(BindingDisplayProperty);
        set => SetValue(BindingDisplayProperty, value);
    }

    /// <summary>Raised when the user captures a new binding. Subscribe to call ViewModel.SetHotkey.</summary>
    public event Action<string, HotkeyBinding>? HotkeyCaptured;

    /// <summary>Raised when the user clicks Clear. Subscribe to call ViewModel.ClearHotkey.</summary>
    public event Action<string>? HotkeyCleared;

    public HotkeyCaptureControl()
    {
        var panel = new DockPanel { VerticalAlignment = VerticalAlignment.Center };

        // Action label
        var label = new TextBlock
        {
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
            Width = 130
        };
        label.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(ActionName))
        {
            Source = this
        });
        DockPanel.SetDock(label, Dock.Left);
        panel.Children.Add(label);

        // Clear button
        _clearButton = new Button
        {
            Content = "Clear",
            Padding = new Thickness(8, 3, 8, 3),
            FontSize = 11,
            Margin = new Thickness(6, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        _clearButton.Click += (_, _) =>
        {
            HotkeyCleared?.Invoke(ActionName);
            UpdateDisplay();
        };
        DockPanel.SetDock(_clearButton, Dock.Right);
        panel.Children.Add(_clearButton);

        // Assign button
        _assignButton = new Button
        {
            Content = "Assign",
            Padding = new Thickness(10, 3, 10, 3),
            FontSize = 11,
            Margin = new Thickness(6, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        _assignButton.Click += StartCaptureHandler;
        DockPanel.SetDock(_assignButton, Dock.Right);
        panel.Children.Add(_assignButton);

        // Display text (center)
        _displayText = new TextBlock
        {
            FontSize = 12,
            FontFamily = new FontFamily("Consolas"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0)
        };
        panel.Children.Add(_displayText);

        Content = panel;

        // Capture events
        PreviewKeyDown += OnPreviewKeyDown;
        PreviewMouseDown += OnPreviewMouseDown;

        UpdateDisplay();
    }

    private static void OnBindingDisplayChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is HotkeyCaptureControl ctrl)
            ctrl.UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (_isCapturing)
        {
            _displayText.Text = "Press keys...";
            _displayText.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0x7A, 0xCC)); // Accent
        }
        else
        {
            var text = BindingDisplay;
            _displayText.Text = text;
            _displayText.Foreground = text == "Not assigned"
                ? new SolidColorBrush(Color.FromRgb(0x85, 0x85, 0x85)) // Dim
                : new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)); // Normal text
        }
    }

    private void StartCapture()
    {
        // Cancel any other control that is currently capturing (mutual exclusion)
        if (_activeCapture != null && _activeCapture != this)
            _activeCapture.CancelCapture();

        _isCapturing = true;
        _activeCapture = this;
        _assignButton.Content = "Cancel";
        _assignButton.Click -= StartCaptureHandler;
        _assignButton.Click += CancelCaptureHandler;
        Focus();
        Keyboard.Focus(this);
        UpdateDisplay();
    }

    private void StopCapture()
    {
        _isCapturing = false;
        if (_activeCapture == this)
            _activeCapture = null;
        _assignButton.Content = "Assign";
        _assignButton.Click -= CancelCaptureHandler;
        _assignButton.Click += StartCaptureHandler;
        UpdateDisplay();
    }

    /// <summary>Cancels an in-progress capture from outside (e.g. another control taking over).</summary>
    public void CancelCapture()
    {
        if (_isCapturing)
            StopCapture();
    }

    private void StartCaptureHandler(object sender, RoutedEventArgs e) => StartCapture();
    private void CancelCaptureHandler(object sender, RoutedEventArgs e) => StopCapture();

    /// <summary>Walks up the visual tree to check if <paramref name="child"/> is within <paramref name="parent"/>.</summary>
    private static bool IsWithinElement(DependencyObject child, DependencyObject parent)
    {
        while (child != null)
        {
            if (child == parent) return true;
            child = System.Windows.Media.VisualTreeHelper.GetParent(child);
        }
        return false;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_isCapturing) return;
        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // Cancel on Escape
        if (key == Key.Escape)
        {
            StopCapture();
            return;
        }

        // Ignore modifier-only presses
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift
            or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin)
            return;

        var modifiers = Keyboard.Modifiers;
        var binding = new HotkeyBinding
        {
            Key = key,
            Modifiers = modifiers,
            MouseButton = HotkeyMouseButton.None
        };

        StopCapture();
        HotkeyCaptured?.Invoke(ActionName, binding);
    }

    private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!_isCapturing) return;

        // Allow clicks on the Assign/Cancel button to pass through so the Click event fires
        if (e.OriginalSource is DependencyObject src && IsWithinElement(src, _assignButton))
            return;

        // Reject left and right mouse buttons
        if (e.ChangedButton is MouseButton.Left or MouseButton.Right)
        {
            e.Handled = true;
            return;
        }

        e.Handled = true;

        HotkeyMouseButton mouseButton = e.ChangedButton switch
        {
            MouseButton.Middle => HotkeyMouseButton.Middle,
            MouseButton.XButton1 => HotkeyMouseButton.XButton1,
            MouseButton.XButton2 => HotkeyMouseButton.XButton2,
            _ => HotkeyMouseButton.None
        };

        if (mouseButton == HotkeyMouseButton.None)
        {
            StopCapture();
            return;
        }

        var modifiers = Keyboard.Modifiers;
        var binding = new HotkeyBinding
        {
            Key = Key.None,
            Modifiers = modifiers,
            MouseButton = mouseButton
        };

        StopCapture();
        HotkeyCaptured?.Invoke(ActionName, binding);
    }
}
