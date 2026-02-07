using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using NSubstitute;
using Xunit;
using CommunityToolkit.Mvvm.Input;
#pragma warning disable xUnit1051
using CollectorUI.ViewModels;
using CollectorUI.Models;
using CollectorUI.Services;

namespace CollectorUI.Tests;

public class MainWindowViewModelServiceTests
{
    [Fact(DisplayName = "GenerateCoverage calls report generator and persists selection")]
    public async Task GenerateCoverage_CallsReportGeneratorAndSavesSelection()
    {
        // Arrange
        var selectionService = Substitute.For<ISelectionService>();
        var reportService = Substitute.For<IReportGeneratorService>();
        var updateService = Substitute.For<IUpdateService>();

        reportService.CreateReportAsync(Arg.Any<string?>(), Arg.Any<List<ProjectModel>>())
            .Returns(ci =>
            {
                var list = ci.ArgAt<List<ProjectModel>>(1);
                if (list.Count > 0)
                {
                    list[0].CoverageReportIndexPath = "/tmp/index.html";
                }
                return Task.FromResult("Success");
            });

        var vm = new MainWindowViewModel(selectionService, reportService, updateService)
        {
            SolutionPath = "/tmp/test.sln"
        };

        var project = new ProjectModel { Name = "P", FullPath = "/tmp/p.csproj", IsTestProject = true, IsSelected = true };
        vm.TestProjects = new ObservableCollection<ProjectModel>(new[] { project });

        // Act
        await vm.GenerateCoverage();

        // Assert
        Assert.Equal("Success", vm.StatusMessage);
        selectionService.Received(1).SaveDeselectedForSolution(Arg.Is<string>(s => s == vm.SolutionPath), Arg.Any<IEnumerable<(string, IEnumerable<string>)>>());
        selectionService.Received(1).UpsertSolutionReport(Arg.Is<string>(s => s == vm.SolutionPath));
    }

    [Fact(DisplayName = "CheckForAppUpdate sets update flags when update available")]
    public async Task CheckForAppUpdate_SetsFlags_WhenAvailable()
    {
        // Arrange
        var selectionService = Substitute.For<ISelectionService>();
        var reportService = Substitute.For<IReportGeneratorService>();
        var updateService = Substitute.For<IUpdateService>();

        var latest = new UpdateService.ReleaseInfo("v99.0.0", "Test Release", new Version(99, 0, 0), "asset.zip", "http://example.com/asset.zip");
        updateService.GetLatestReleaseAsync(Arg.Any<System.Threading.CancellationToken>()).Returns(Task.FromResult<UpdateService.ReleaseInfo?>(latest));
        updateService.IsUpdateAvailable(Arg.Any<Version>()).Returns(true);

        var vm = new MainWindowViewModel(selectionService, reportService, updateService);

        // Act
        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)vm.CheckForAppUpdateCommand).ExecuteAsync(null);

        // Assert
        Assert.True(vm.IsUpdateAvailable);
        Assert.Equal("v99.0.0", vm.LatestVersion);
    }

    [Fact(DisplayName = "DownloadAndRunUpdate invokes updater when available")]
    public async Task DownloadAndRunUpdate_InvokesUpdater_WhenSuccessful()
    {
        // Arrange
        var selectionService = Substitute.For<ISelectionService>();
        var reportService = Substitute.For<IReportGeneratorService>();
        var updateService = Substitute.For<IUpdateService>();

        var latest = new UpdateService.ReleaseInfo("v99.0.0", "Test Release", new Version(99, 0, 0), "asset.zip", "http://example.com/asset.zip");
        updateService.GetLatestReleaseAsync(Arg.Any<System.Threading.CancellationToken>()).Returns(Task.FromResult<UpdateService.ReleaseInfo?>(latest));
        updateService.IsUpdateAvailable(Arg.Any<Version>()).Returns(true);
        updateService.DownloadAssetAsync(Arg.Any<string>(), Arg.Any<IProgress<double>>(), Arg.Any<System.Threading.CancellationToken>()).Returns(Task.FromResult("/tmp/asset.zip"));
        updateService.ExtractToNewFolder(Arg.Any<string>(), Arg.Any<string>()).Returns("/tmp/extracted");
        updateService.StartUpdaterAndExit(Arg.Any<string>()).Returns(true);

        var vm = new MainWindowViewModel(selectionService, reportService, updateService);

        // Act
        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)vm.DownloadAndRunUpdateCommand).ExecuteAsync(null);

        // Assert
        await updateService.Received(1).DownloadAssetAsync(Arg.Is<string>(s => s == latest.AssetDownloadUrl), Arg.Any<IProgress<double>>());
        updateService.Received(1).StartUpdaterAndExit(Arg.Is<string>(s => s == "/tmp/extracted"));
        Assert.Contains("Updater launched", vm.StatusMessage);
    }
}
#pragma warning restore xUnit1051