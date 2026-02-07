using System.Collections.ObjectModel;
using Xunit;
using CollectorUI.ViewModels;
using CollectorUI.Models;

namespace CollectorUI.Tests;

public class FakeSelectionService : CollectorUI.Services.ISelectionService
{
    public HashSet<string> LoadDeselectedNamespaces(string solutionPath, string projectPath) => new();

    public void SaveDeselectedForSolution(string solutionPath, IEnumerable<(string ProjectPath, IEnumerable<string> DeselectedNamespaces)> data) { }

    public void UpsertSolutionReport(string solutionPath) { }

    public IReadOnlyList<string> GetRecentSolutions(int limit = 10) => new List<string>();

    public void RemoveSolutionRecords(string solutionPath) { }
}

public class MainWindowViewModelTests
{
    [Fact(DisplayName = "SelectAllProjects selects all projects")]
    public void SelectAllProjects_MultipleProjects_AllSelected()
    {
        var vm = new MainWindowViewModel(new FakeSelectionService());
        var projects = new List<ProjectModel>
        {
            new ProjectModel { Name = "A", IsSelected = false },
            new ProjectModel { Name = "B", IsSelected = false },
            new ProjectModel { Name = "C", IsSelected = false }
        };

        vm.TestProjects = new ObservableCollection<ProjectModel>(projects);

        vm.SelectAllProjects();

        Assert.All(vm.TestProjects, p => Assert.True(p.IsSelected));
    }

    [Fact(DisplayName = "UnselectAllProjects unselects all projects")]
    public void UnselectAllProjects_MultipleProjects_AllUnselected()
    {
        var vm = new MainWindowViewModel(new FakeSelectionService());
        var projects = new List<ProjectModel>
        {
            new ProjectModel { Name = "A", IsSelected = true },
            new ProjectModel { Name = "B", IsSelected = true },
            new ProjectModel { Name = "C", IsSelected = true }
        };

        vm.TestProjects = new ObservableCollection<ProjectModel>(projects);

        vm.UnselectAllProjects();

        Assert.All(vm.TestProjects, p => Assert.False(p.IsSelected));
    }
}