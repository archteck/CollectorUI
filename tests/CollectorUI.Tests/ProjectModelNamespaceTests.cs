using System.Collections.Generic;
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
