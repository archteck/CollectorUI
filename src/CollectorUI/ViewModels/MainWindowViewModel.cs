using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CollectorUI.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CollectorUI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly Services.ISelectionService _selectionService;
    private readonly Services.IReportGeneratorService _reportGeneratorService;
    private readonly Services.IUpdateService _updateService;
    private CancellationTokenSource? _operationCts;

    [ObservableProperty] private string? _solutionPath;

    [ObservableProperty] private SolutionModel? _currentSolution;
    public void Dispose()
    {
        EndCancelableOperation();
        GC.SuppressFinalize(this);
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTestProjects))]
    [NotifyPropertyChangedFor(nameof(HasNoTestProjects))]
    private ObservableCollection<ProjectModel> _testProjects = new();

    [ObservableProperty] private bool _isBusy;

    [ObservableProperty] private string? _statusMessage;

    [ObservableProperty] private bool _canGenerateCoverage;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CancelOperationCommand))]
    private bool _isOperationCancelable;

    // App update related
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanDownloadAppUpdate))]
    private string _latestVersion = "";

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanDownloadAppUpdate))]
    private bool _isUpdateAvailable;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanDownloadAppUpdate))]
    private bool _isCheckingUpdate;

    [ObservableProperty] private double _downloadProgress; // 0..1

    public bool CanDownloadAppUpdate => IsUpdateAvailable && !IsCheckingUpdate;

    public bool HasTestProjects => TestProjects.Count > 0;

    public bool HasNoTestProjects => !HasTestProjects;

    public MainWindowViewModel(
        Services.ISelectionService? selectionService = null,
        Services.IReportGeneratorService? reportGeneratorService = null,
        Services.IUpdateService? updateService = null)
    {
        _selectionService = selectionService ?? new Services.SelectionService();
        _reportGeneratorService = reportGeneratorService ?? new Services.ReportGeneratorServiceWrapper();
        _updateService = updateService ?? new Services.UpdateServiceWrapper();
        SolutionPath = "Please select a solution file (.sln/.slnx)";
    }

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
            var cancellationToken = BeginCancelableOperation();
            IsBusy = true;
            StatusMessage = "Loading solution...";
            SolutionPath = path;

            var parsed = await Task.Run(() => SolutionModel.ParseFromFile(path, cancellationToken), cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            // Atribuições em thread da UI
            CurrentSolution = parsed;
            TestProjects = new ObservableCollection<ProjectModel>(parsed.TestProjects);

            // Aplica estados desmarcados guardados por solução
            foreach (var project in TestProjects)
            {
                project.ApplyDeselectionStates(SolutionPath!, _selectionService);
            }

            StatusMessage = $"Loaded solution with {TestProjects.Count} test projects";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Operation canceled.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading solution: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            EndCancelableOperation();
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
            var cancellationToken = BeginCancelableOperation();
            IsBusy = true;
            StatusMessage = "Generating coverage reports...";
            var result = await _reportGeneratorService.CreateReportAsync(SolutionPath, TestProjects.ToList(), cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            // Força o ItemsControl a atualizar para refletir HasCoverageReport/paths
            TestProjects = new ObservableCollection<ProjectModel>(TestProjects);

            // Persiste os últimos checks (desmarcados) por solução/projeto
            _selectionService.SaveDeselectedForSolution(SolutionPath!, TestProjects.Select(p =>
                (p.FullPath ?? string.Empty, p.GetDeselectedNamespaces())));

            // Atualiza a lista de recentes baseada em reports gerados
            if (!string.IsNullOrWhiteSpace(SolutionPath) && TestProjects.Any(p => p.HasCoverageReport))
            {
                _selectionService.UpsertSolutionReport(SolutionPath!);
            }

            StatusMessage = result;
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Operation canceled.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error generating coverage: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            EndCancelableOperation();
        }
    }

    [RelayCommand(CanExecute = nameof(IsOperationCancelable))]
    public void CancelOperation()
    {
        if (_operationCts is null || _operationCts.IsCancellationRequested)
        {
            return;
        }

        StatusMessage = "Cancelling current operation...";
        _operationCts.Cancel();
    }

    private CancellationToken BeginCancelableOperation()
    {
        EndCancelableOperation();

        _operationCts = new CancellationTokenSource();
        IsOperationCancelable = true;

        return _operationCts.Token;
    }

    private void EndCancelableOperation()
    {
        IsOperationCancelable = false;

        if (_operationCts is null)
        {
            return;
        }

        _operationCts.Dispose();
        _operationCts = null;
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
        if (project?.HasCoverageReport == true && File.Exists(project.CoverageReportIndexPath))
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
            var picker = new Views.Dialogs.SelectRecentDialog();
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
            var confirm = new Views.Dialogs.ConfirmDialog(
                "File not found",
                $"The solution file was not found:\n{selected}\n\nRemove it from recent and database?");
            var remove = await confirm.ShowDialog<bool>(window);

            if (remove)
            {
                _selectionService.RemoveSolutionRecords(selected);
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
            var latest = await _updateService.GetLatestReleaseAsync();
            if (latest is null)
            {
                StatusMessage = "No suitable release asset found for your platform.";
                IsUpdateAvailable = false;
                LatestVersion = string.Empty;
                return;
            }

            LatestVersion = latest.TagName;
            IsUpdateAvailable = _updateService.IsUpdateAvailable(latest.Version);
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
            var latest = await _updateService.GetLatestReleaseAsync();
            if (latest is null)
            {
                StatusMessage = "No suitable release asset found.";
                return;
            }
            if (!_updateService.IsUpdateAvailable(latest.Version))
            {
                StatusMessage = "Already up to date.";
                return;
            }

            var progress = new Progress<double>(p => { DownloadProgress = p; });
            var zipPath = await _updateService.DownloadAssetAsync(latest.AssetDownloadUrl, progress);
            StatusMessage = "Download complete. Extracting...";
            var extractedDir = _updateService.ExtractToNewFolder(zipPath, latest.TagName);
            // Hand off to external updater which will copy files into current app folder and relaunch
            var launched = _updateService.StartUpdaterAndExit(extractedDir);
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
