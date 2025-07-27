using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CollectorUI.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CollectorUI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty] private string? _solutionPath;

    [ObservableProperty] private SolutionModel? _currentSolution;

    [ObservableProperty] private ObservableCollection<ProjectModel> _testProjects = new();

    [ObservableProperty] private bool _isBusy;

    [ObservableProperty] private string? _statusMessage;

    public MainWindowViewModel() => SolutionPath = "Please select a solution file (.slnx)";

    [RelayCommand]
    public async Task SelectSolutionAsync()
    {
        var window = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (window is null)
        {
            return;
        }

        var result = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
        {
            AllowMultiple = false,
            Title = "Select Solution File",
            FileTypeFilter = [
                new FilePickerFileType("Solution Files")
                {
                    Patterns = ["*.slnx"]
                }
            ]
        });
        if (result.Count > 0)
        {
            await LoadSolutionAsync(result[0].Path.LocalPath);
        }
    }

    [RelayCommand]
    public async Task LoadSolutionAsync(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            StatusMessage = "Invalid solution file path";
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Loading solution...";
            SolutionPath = path;

            await Task.Run(() =>
            {
                CurrentSolution = SolutionModel.ParseFromFile(path);
                TestProjects = new ObservableCollection<ProjectModel>(CurrentSolution.TestProjects);
            });

            StatusMessage = $"Loaded solution with {TestProjects.Count} test projects";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading solution: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public void SelectAllProjects()
    {
        foreach (var project in TestProjects)
        {
            project.IsSelected = true;
        }
    }

    [RelayCommand]
    public void UnselectAllProjects()
    {
        foreach (var project in TestProjects)
        {
            project.IsSelected = false;
        }
    }

    /// <summary>
    /// Propriedade auxiliar para expor a árvore de namespaces do projeto selecionado.
    /// </summary>
    public ObservableCollection<NamespaceNodeViewModel>? SelectedProjectNamespaceTree
    {
        get
        {
            var selected = TestProjects.FirstOrDefault(p => p.IsSelected);
            return selected is not null ? new ObservableCollection<NamespaceNodeViewModel>(selected.GetNamespaceTree()) : null;
        }
    }
}
