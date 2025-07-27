using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Path = Avalonia.Controls.Shapes.Path;

namespace CollectorUI.Views;

public partial class MainWindow : Window
{
    private Path? _maximizeIcon;
    private Grid? _titleBar;

    public MainWindow() => InitializeComponent();

    #region BaseWindow overrides

    protected override void OnResized(WindowResizedEventArgs e)
    {
        base.OnResized(e);
        if (e.ClientSize.Height <= 550)
        {
            Height = 550;
        }
        if (e.ClientSize.Width <= 550)
        {
            Width = 550;
        }
    }
    #endregion

}
