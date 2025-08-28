using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Path = Avalonia.Controls.Shapes.Path;

namespace CollectorUI.Controls;

public partial class TitleBar : UserControl
{
    private Window? _parentWindow;
    private Path? _maximizeIcon;

    public static readonly StyledProperty<string> WindowTitleProperty =
        AvaloniaProperty.Register<TitleBar, string>(nameof(WindowTitle), "Window");

    public string WindowTitle
    {
        get => GetValue(WindowTitleProperty);
        set => SetValue(WindowTitleProperty, value);
    }

    public TitleBar()
    {
        InitializeComponent();

        // This is important - set DataContext to this so the bindings work
        DataContext = this;

        // Find the MaximizeIcon control
        _maximizeIcon = this.FindControl<Path>("MaximizeIcon");

        // Hook up button click events
        this.FindControl<Button>("MinimizeButton")!.Click += MinimizeButton_Click;
        this.FindControl<Button>("MaximizeButton")!.Click += MaximizeButton_Click;
        this.FindControl<Button>("CloseButton")!.Click += CloseButton_Click;

        // Make the title bar draggable
        this.FindControl<Grid>("TitleBarGrid")!.PointerPressed += TitleBar_PointerPressed;

        Loaded += TitleBar_Loaded;
    }

    private void TitleBar_Loaded(object? sender, RoutedEventArgs e)
    {
        // Get a reference to the parent window when loaded
        _parentWindow = VisualRoot as Window;

        if (_parentWindow != null)
        {
            // Set the WindowTitle if it hasn't been explicitly set
            if (string.IsNullOrEmpty(WindowTitle) || WindowTitle == "Window")
            {
                WindowTitle = _parentWindow.Title ?? "Window";
            }

            // Listen for window state changes to update the maximize icon
            _parentWindow.PropertyChanged += (s, args) =>
            {
                if (args.Property == Window.WindowStateProperty)
                {
                    UpdateMaximizeRestoreIcon();
                }
            };

            // Initialize icon state
            UpdateMaximizeRestoreIcon();
        }
    }

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_parentWindow == null)
        {
            return;
        }

        if (e.GetCurrentPoint(_parentWindow).Properties.IsLeftButtonPressed)
        {
            // If window is maximized, we need to restore it before dragging
            if (_parentWindow.WindowState == WindowState.Maximized)
            {
                // Get the mouse position relative to the window
                var position = e.GetPosition(_parentWindow);

                // Restore the window
                _parentWindow.WindowState = WindowState.Normal;

                // Adjust window position to make it appear under the cursor
                _parentWindow.Position = new PixelPoint(
                    (int)(e.GetPosition(null).X - (position.X * _parentWindow.Width / _parentWindow.Bounds.Width)),
                    0);
            }

            // Begin the drag operation
            _parentWindow.BeginMoveDrag(e);
        }
    }

    private void MinimizeButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_parentWindow != null)
        {
            _parentWindow.WindowState = WindowState.Minimized;
        }
    }

    private void MaximizeButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_parentWindow != null)
        {
            _parentWindow.WindowState = _parentWindow.WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e) => _parentWindow?.Close();

    private void UpdateMaximizeRestoreIcon()
    {
        if (_maximizeIcon != null && _parentWindow != null)
        {
            // Use a different icon depending on whether the window is maximized
            if (_parentWindow.WindowState == WindowState.Maximized)
            {
                _maximizeIcon.Data = Geometry.Parse("M 0,2 H 8 V 10 H 0 Z M 2,0 H 10 V 8 H 8 V 2 H 2 Z");
            }
            else
            {
                _maximizeIcon.Data = Geometry.Parse("M 0,0 H 10 V 10 H 0 Z");
            }
        }
    }
}
