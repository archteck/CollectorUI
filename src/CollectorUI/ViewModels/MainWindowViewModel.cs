using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CollectorUI.Models;
using CollectorUI.Services;
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

    [ObservableProperty] private bool _canGenerateCoverage;

    // App update related
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanDownloadAppUpdate))]
    private string _latestVersion = "";

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanDownloadAppUpdate))]
    private bool _isUpdateAvailable;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanDownloadAppUpdate))]
    private bool _isCheckingUpdate;

    [ObservableProperty] private double _downloadProgress; // 0..1

    public bool CanDownloadAppUpdate => IsUpdateAvailable && !IsCheckingUpdate;
    public MainWindowViewModel() => SolutionPath = "Please select a solution file (.sln/.slnx)";

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
            FileTypeFilter =
            [
                new FilePickerFileType("Solution Files") { Patterns = ["*.sln", "*.slnx"] }
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

            var parsed = await Task.Run(() => SolutionModel.ParseFromFile(path));

            // Atribuições em thread da UI
            CurrentSolution = parsed;
            TestProjects = new ObservableCollection<ProjectModel>(parsed.TestProjects);

            // Aplica estados desmarcados guardados por solução
            foreach (var project in TestProjects)
            {
                project.ApplyDeselectionStates(SolutionPath!);
            }

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

        // Força o ItemsControl a atualizar
        TestProjects = new ObservableCollection<ProjectModel>(TestProjects);
    }

    [RelayCommand]
    public void UnselectAllProjects()
    {
        foreach (var project in TestProjects)
        {
            project.IsSelected = false;
        }

        // Força o ItemsControl a atualizar
        TestProjects = new ObservableCollection<ProjectModel>(TestProjects);
    }

    [RelayCommand]
    public async Task GenerateCoverage()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Generating coverage reports...";
            var result = await ReportGeneratorService.CreateReportAsync(SolutionPath, TestProjects.ToList());

            // Força o ItemsControl a atualizar para refletir HasCoverageReport/paths
            TestProjects = new ObservableCollection<ProjectModel>(TestProjects);

            // Persiste os últimos checks (desmarcados) por solução/projeto
            SelectionService.SaveDeselectedForSolution(SolutionPath!, TestProjects.Select(p =>
                (p.FullPath ?? string.Empty, p.GetDeselectedNamespaces())));

            // Atualiza a lista de recentes baseada em reports gerados
            if (!string.IsNullOrWhiteSpace(SolutionPath) && TestProjects.Any(p => p.HasCoverageReport))
            {
                SelectionService.UpsertSolutionReport(SolutionPath!);
            }

            StatusMessage = result;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error generating coverage: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnTestProjectsChanged(ObservableCollection<ProjectModel> value) => UpdateCanGenerateCoverage();

    partial void OnSolutionPathChanged(string? value) => UpdateCanGenerateCoverage();

    private void UpdateCanGenerateCoverage() => CanGenerateCoverage = TestProjects.Count > 0 &&
                                                                      !string.IsNullOrWhiteSpace(SolutionPath) &&
                                                                      (SolutionPath.EndsWith(".sln",
                                                                           StringComparison.OrdinalIgnoreCase) ||
                                                                       SolutionPath.EndsWith(".slnx",
                                                                           StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Propriedade auxiliar para expor a árvore de namespaces do projeto selecionado.
    /// </summary>
    public ObservableCollection<NamespaceNodeViewModel>? SelectedProjectNamespaceTree
    {
        get
        {
            var selected = TestProjects.FirstOrDefault(p => p.IsSelected);
            return selected is not null
                ? new ObservableCollection<NamespaceNodeViewModel>(selected.GetNamespaceTree())
                : null;
        }
    }

    [RelayCommand]
    public void OpenReport(ProjectModel? project)
    {
        if (project?.HasCoverageReport == true && System.IO.File.Exists(project.CoverageReportIndexPath))
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = project.CoverageReportIndexPath, UseShellExecute = true
                });
                StatusMessage = $"Opened report for {project.Name}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to open report: {ex.Message}";
            }
        }
        else
        {
            StatusMessage = "Report not found for this project.";
        }
    }

    [RelayCommand]
    public async Task LoadRecentSolutionsAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Loading recent solutions...";

            var window = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)
                ?.MainWindow;
            if (window is null)
            {
                StatusMessage = "No main window available.";
                return;
            }

            // Abre o diálogo para o utilizador escolher uma solução recente
            var picker = new CollectorUI.Views.Dialogs.SelectRecentDialog();
            var selected = await picker.ShowDialog<string?>(window);

            if (string.IsNullOrWhiteSpace(selected))
            {
                StatusMessage = "No recent solution selected.";
                return;
            }

            if (File.Exists(selected))
            {
                await LoadSolutionAsync(selected);
                return;
            }

            // Se não existir, perguntar se pretende remover da lista/BD
            var confirm = new CollectorUI.Views.Dialogs.ConfirmDialog(
                "File not found",
                $"The solution file was not found:\n{selected}\n\nRemove it from recent and database?",
                "Yes",
                "No");
            var remove = await confirm.ShowDialog<bool>(window);

            if (remove)
            {
                SelectionService.RemoveSolutionRecords(selected);
                StatusMessage = "Removed missing solution from database.";
            }
            else
            {
                StatusMessage = "Solution file not found.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading recent solutions: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CheckForAppUpdate()
    {
        try
        {
            IsCheckingUpdate = true;
            StatusMessage = "Checking for app updates...";
            var latest = await UpdateService.GetLatestReleaseAsync();
            if (latest is null)
            {
                StatusMessage = "No suitable release asset found for your platform.";
                IsUpdateAvailable = false;
                LatestVersion = string.Empty;
                return;
            }

            LatestVersion = latest.TagName;
            IsUpdateAvailable = UpdateService.IsUpdateAvailable(latest.Version);
            StatusMessage = IsUpdateAvailable
                ? $"Update available: {latest.TagName} (asset: {latest.AssetName})."
                : "You're on the latest version.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Update check failed: {ex.Message}";
            IsUpdateAvailable = false;
        }
        finally
        {
            IsCheckingUpdate = false;
        }
    }

    [RelayCommand]
    private async Task DownloadAndRunUpdate()
    {
        try
        {
            StatusMessage = "Preparing download...";
            var latest = await UpdateService.GetLatestReleaseAsync();
            if (latest is null)
            {
                StatusMessage = "No suitable release asset found.";
                return;
            }
            if (!UpdateService.IsUpdateAvailable(latest.Version))
            {
                StatusMessage = "Already up to date.";
                return;
            }

            var progress = new Progress<double>(p => { DownloadProgress = p; });
            var zipPath = await UpdateService.DownloadAssetAsync(latest.AssetDownloadUrl, progress);
            StatusMessage = "Download complete. Extracting...";
            var extractedDir = UpdateService.ExtractToNewFolder(zipPath, latest.TagName);
            // Hand off to external updater which will copy files into current app folder and relaunch
            var launched = UpdateService.StartUpdaterAndExit(extractedDir);
            if (launched)
            {
                StatusMessage = "Updater launched. The application will close and restart after updating.";
            }
            else
            {
                StatusMessage = $"Update extracted to {extractedDir}, but failed to start updater. Please update manually.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Update failed: {ex.Message}";
        }
    }
}
