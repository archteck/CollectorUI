using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CollectorUI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CollectorUI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty] private string _statusText = string.Empty;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsGeneratorButtonEnabled))]
    private string _testProjectFolder = string.Empty;

    private readonly ReportGeneratorService _reportGeneratorService = new();
    public bool IsGeneratorButtonEnabled => !string.IsNullOrEmpty(TestProjectFolder);

    [RelayCommand]
    private async Task BrowseFolder()
    {
        var window = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (window is null)
        {
            return;
        }

        StatusText = "Browse Folder clicked!\n";
        var folder = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false
        });

        if (folder.Count > 0)
        {
            TestProjectFolder = folder[0].Path.LocalPath;
            StatusText = "Browse Folder selected -> " + folder[0].Path;
        }
    }

    [RelayCommand]
    private async Task GenerateReport()
    {
        StatusText = "Generating report...";
        // StatusText = await _reportGeneratorService.GenerateReport(DestinationFolder, clean: false);
    }
}
