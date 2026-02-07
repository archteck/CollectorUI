using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using CollectorUI.Services;

namespace CollectorUI.Views;

public partial class MainWindow : Window
{
    // Guarda o último tamanho em estado Normal (para não salvar dimensões maximizadas).
    private double _lastNormalWidth;
    private double _lastNormalHeight;

    public MainWindow()
    {
        InitializeComponent();

        // Carrega tamanho salvo (aplica mínimos).
        var savedWidth = SettingsService.GetDouble("Window.Width", Width);
        var savedHeight = SettingsService.GetDouble("Window.Height", Height);
        Width = Math.Max(600, savedWidth);
        Height = Math.Max(600, savedHeight);

        // Inicializa o último tamanho normal com os atuais.
        _lastNormalWidth = Width;
        _lastNormalHeight = Height;

        // Keep PropertyChanged handler for window state changes (system decorations handle visuals)
        PropertyChanged += (_, args) =>
        {
            if (args.Property == WindowStateProperty)
            {
                // No-op: system title bar provides maximize/minimize visuals
            }
        };

        // Guardar tamanho ao fechar
        Closing += (_, __) => SaveWindowSize();
    }


    protected override void OnResized(WindowResizedEventArgs e)
    {
        base.OnResized(e);

        // Enforce mínimos
        if (e.ClientSize.Height <= 600)
        {
            Height = 600;
        }
        if (e.ClientSize.Width <= 600)
        {
            Width = 600;
        }

        // Memoriza último tamanho apenas quando em estado normal
        if (WindowState == WindowState.Normal)
        {
            _lastNormalWidth = Width;
            _lastNormalHeight = Height;
        }
    }






    private void SaveWindowSize()
    {
        // Se estiver normal, usa tamanho atual; caso contrário, usa último tamanho normal memorizado.
        var w = WindowState == WindowState.Normal ? Width : _lastNormalWidth;
        var h = WindowState == WindowState.Normal ? Height : _lastNormalHeight;

        // Respeita mínimos
        w = Math.Max(600, w);
        h = Math.Max(600, h);

        SettingsService.SetDouble("Window.Width", w);
        SettingsService.SetDouble("Window.Height", h);
    }

    private void Exit_OnClick(object sender, RoutedEventArgs e) => Close();
}
