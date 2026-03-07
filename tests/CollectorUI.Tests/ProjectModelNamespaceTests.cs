using CollectorUI.Models;
using CollectorUI.Services;
using CollectorUI.ViewModels;
using Xunit;

namespace CollectorUI.Tests;

public class ProjectModelNamespaceTests
{
    [Fact(DisplayName = "Filter keeps namespace selection states")]
    public void FilterText_FilterAndClear_PreservesSelectionState()
    {
        var model = CreateModelWithSampleNamespaces();

        var node = FindNode(model.NamespaceTree, "Acme.Feature");
        Assert.NotNull(node);

        node!.IsChecked = false;

        model.FilterText = "Feature";
        model.FilterText = null;

        var rebuilt = FindNode(model.NamespaceTree, "Acme.Feature");
        Assert.NotNull(rebuilt);
        Assert.False(rebuilt!.IsChecked);
    }

    [Fact(DisplayName = "Selection rollup keeps parent/child semantics")]
    public void GetSelectedNamespaces_PartialSelection_ReturnsMinimalSelectedBranches()
    {
        var model = CreateModelWithSampleNamespaces();

        var feature = FindNode(model.NamespaceTree, "Acme.Feature");
        Assert.NotNull(feature);

        feature!.IsChecked = false;

        var selected = model.GetSelectedNamespaces();

        Assert.Contains("Acme.Other", selected);
        Assert.DoesNotContain("Acme", selected);
        Assert.DoesNotContain("Acme.Feature", selected);
    }

    [Fact(DisplayName = "Saved deselections are applied to tree")]
    public void ApplyDeselectionStates_DeselectedNamespacesExist_MarksNodesUnchecked()
    {
        var model = CreateModelWithSampleNamespaces();
        var selectionService = new StubSelectionService(["Acme.Feature"]);

        model.ApplyDeselectionStates("/tmp/sample.sln", selectionService);

        var feature = FindNode(model.NamespaceTree, "Acme.Feature");
        Assert.NotNull(feature);
        Assert.False(feature!.IsChecked);
    }

    [Fact(DisplayName = "Test project falls back to own namespaces when dependencies are empty")]
    public void GetNamespaceTree_TestProjectWithoutDependencyNamespaces_UsesOwnNamespaces()
    {
        var model = new ProjectModel
        {
            IsTestProject = true,
            Namespaces =
            [
                new NamespaceModel { Name = "Standalone.Tests" },
                new NamespaceModel { Name = "Standalone.Tests.Unit" }
            ]
        };

        model.BuildNamespaceTree();

        Assert.NotEmpty(model.NamespaceTree);
        var root = FindNode(model.NamespaceTree, "Standalone");
        Assert.NotNull(root);
    }

    [Fact(DisplayName = "Large namespace set keeps tree functional during filtering")]
    public void FilterText_LargeNamespaceSet_RebuildsTreeWithoutLosingData()
    {
        var model = new ProjectModel
        {
            IsTestProject = false,
            Namespaces = CreateLargeNamespaceSet(2000)
        };

        model.BuildNamespaceTree();
        Assert.NotEmpty(model.NamespaceTree);

        model.FilterText = "Module42";
        Assert.NotEmpty(model.NamespaceTree);

        model.FilterText = null;
        Assert.NotEmpty(model.NamespaceTree);
    }

    private static ProjectModel CreateModelWithSampleNamespaces()
    {
        var model = new ProjectModel
        {
            FullPath = "/tmp/sample.csproj",
            IsTestProject = false,
            Namespaces =
            [
                new NamespaceModel { Name = "Acme.Feature" },
                new NamespaceModel { Name = "Acme.Feature.Sub" },
                new NamespaceModel { Name = "Acme.Other" }
            ]
        };

        model.BuildNamespaceTree();
        return model;
    }

    private static List<NamespaceModel> CreateLargeNamespaceSet(int count)
    {
        var list = new List<NamespaceModel>(capacity: count);
        for (int i = 0; i < count; i++)
        {
            list.Add(new NamespaceModel { Name = $"Acme.Module{i}.Feature{i % 10}.Part{i % 5}" });
        }

        return list;
    }

    private static NamespaceNodeViewModel? FindNode(IEnumerable<NamespaceNodeViewModel> roots, string name)
    {
        foreach (var root in roots)
        {
            var found = FindNodeRecursive(root, name);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static NamespaceNodeViewModel? FindNodeRecursive(NamespaceNodeViewModel node, string name)
    {
        if (node.Name == name)
        {
            return node;
        }

        foreach (var child in node.Children)
        {
            var found = FindNodeRecursive(child, name);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private sealed class StubSelectionService(HashSet<string> deselected) : ISelectionService
    {
        public HashSet<string> LoadDeselectedNamespaces(string solutionPath, string projectPath) => deselected;

        public void SaveDeselectedForSolution(string solutionPath, IEnumerable<(string ProjectPath, IEnumerable<string> DeselectedNamespaces)> data)
        {
        }

        public void UpsertSolutionReport(string solutionPath)
        {
        }

        public IReadOnlyList<string> GetRecentSolutions(int limit = 10) => [];

        public void RemoveSolutionRecords(string solutionPath)
        {
        }
    }
}
