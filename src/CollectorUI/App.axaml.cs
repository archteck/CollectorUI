using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CollectorUI.Views;
using Microsoft.Extensions.DependencyInjection;

namespace CollectorUI;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Configure DI
            var services = new ServiceCollection();
            services.AddSingleton<Services.ISelectionService, Services.SelectionService>();
            services.AddSingleton<Services.IReportGeneratorService, Services.ReportGeneratorServiceWrapper>();
            services.AddSingleton<Services.IUpdateService, Services.UpdateServiceWrapper>();
            services.AddSingleton<ViewModels.MainWindowViewModel>();

            var provider = services.BuildServiceProvider();

            desktop.MainWindow = new MainWindow
            {
                DataContext = provider.GetRequiredService<ViewModels.MainWindowViewModel>(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
