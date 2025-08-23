using System.Xml.Linq;
using CollectorUI.ViewModels;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;

namespace CollectorUI.Models;

public class ProjectModel
{
    public string? Name { get; set; }
    public string? FullPath { get; set; }
    public bool IsTestProject { get; set; }
    public List<string> ProjectReferences { get; set; } = [];
    public List<ProjectModel> Dependencies { get; set; } = [];
    public List<NamespaceModel> Namespaces { get; set; } = [];
    public bool IsSelected { get; set; } = true;

    // Path to generated HTML coverage report (index.html)
    public string? CoverageReportIndexPath { get; set; }
    public bool HasCoverageReport => !string.IsNullOrWhiteSpace(CoverageReportIndexPath);

    /// <summary>
    /// Árvore de namespaces para binding no XAML.
    /// </summary>
    public ObservableCollection<NamespaceNodeViewModel> NamespaceTree { get; }

    public HierarchicalTreeDataGridSource<NamespaceNodeViewModel>? Source { get; set; }

    private static readonly char[] s_separator = ['\r', '\n'];

    public static ProjectModel FromProjectFile(string projectPath)
    {
        var project = new ProjectModel { FullPath = projectPath, Name = Path.GetFileNameWithoutExtension(projectPath) };

        try
        {
            var doc = XDocument.Load(projectPath);
            var ns = doc.Root?.GetDefaultNamespace();

            // Check if it's a test project based on naming convention
            if (project.Name != null)
            {
                project.IsTestProject = project.Name.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase) ||
                                        project.Name.EndsWith(".Test", StringComparison.OrdinalIgnoreCase) ||
                                        project.Name.EndsWith(".Testing", StringComparison.OrdinalIgnoreCase) ||
                                        project.Name.Contains("Test");
            }

            // Also check for test-related package references
            if (ns != null)
            {
                var packageRefs = doc.Descendants(ns + "PackageReference");
                foreach (var packageRef in packageRefs)
                {
                    var packageName = packageRef.Attribute("Include")?.Value;
                    if (packageName != null &&
                        (packageName.Contains("xunit") ||
                         packageName.Contains("NUnit") ||
                         packageName.Contains("MSTest")))
                    {
                        project.IsTestProject = true;
                        break;
                    }
                }
            }

            // Get project references
            if (ns != null)
            {
                var projectRefs = doc.Descendants(ns + "ProjectReference");
                foreach (var projectRef in projectRefs)
                {
                    var includePath = projectRef.Attribute("Include")?.Value;
                    if (!string.IsNullOrEmpty(includePath))
                    {
                        var fullPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(projectPath) ?? string.Empty,
                            includePath));
                        project.ProjectReferences.Add(fullPath);
                    }
                }
            }

            // Parse namespaces from source files
            project.Namespaces = ExtractNamespaces(projectPath);
            project.BuildNamespaceTree();
        }
        catch (Exception ex)
        {
            // Log or handle the exception as needed
            Console.WriteLine($"Error parsing project {projectPath}: {ex.Message}");
        }

        return project;
    }

    public void FindDependencies(List<ProjectModel> allProjects)
    {
        foreach (var reference in ProjectReferences)
        {
            var referencedProject = allProjects.FirstOrDefault(p =>
                string.Equals(p.FullPath, reference, StringComparison.OrdinalIgnoreCase));

            if (referencedProject != null)
            {
                Dependencies.Add(referencedProject);
            }
        }
    }

    public IEnumerable<ProjectModel> GetAllDependenciesRecursive()
    {
        var visited = new HashSet<ProjectModel>();
        var stack = new Stack<ProjectModel>(Dependencies);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (visited.Add(current))
            {
                foreach (var dep in current.Dependencies)
                {
                    stack.Push(dep);
                }
            }
        }

        return visited;
    }

    private static List<NamespaceModel> ExtractNamespaces(string projectPath)
    {
        var namespaces = new HashSet<string>();
        var projectDir = Path.GetDirectoryName(projectPath);

        if (projectDir == null)
        {
            return [];
        }

        // Find all .cs files in the project directory and subdirectories
        foreach (var file in Directory.GetFiles(projectDir, "*.cs", SearchOption.AllDirectories))
        {
            try
            {
                var content = File.ReadAllText(file);
                var nsLine = content.Split(s_separator, StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault(line =>
                        line.TrimStart().StartsWith("namespace ", StringComparison.OrdinalIgnoreCase));

                if (nsLine != null)
                {
                    var ns = nsLine.TrimStart()[10..].TrimEnd(';', ' ', '{');
                    namespaces.Add(ns);
                }
            }
            catch
            {
                // Skip files that can't be read
            }
        }

        return namespaces
            .OrderBy(ns => ns)
            .Select(ns => new NamespaceModel { Name = ns })
            .ToList();
    }

    /// <summary>
    /// Constrói a árvore de namespaces deste projeto para uso em TreeView.
    /// </summary>
    public List<NamespaceNodeViewModel> GetNamespaceTree()
    {
        // Determina quais namespaces exibir:
        // - Para projetos de teste: união dos namespaces de todos os projetos referenciados (inclusive transitivos).
        // - Para projetos normais: os próprios namespaces do projeto.
        var nsStrings = new HashSet<string>(StringComparer.Ordinal);

        if (IsTestProject)
        {
            foreach (var dep in GetAllDependenciesRecursive())
            {
                foreach (var depNs in dep.Namespaces)
                {
                    if (!string.IsNullOrWhiteSpace(depNs.Name))
                    {
                        nsStrings.Add(depNs.Name!);
                    }
                }
            }
        }
        else
        {
            foreach (var ns in Namespaces)
            {
                if (!string.IsNullOrWhiteSpace(ns.Name))
                {
                    nsStrings.Add(ns.Name!);
                }
            }
        }

        // Agrupa namespaces por hierarquia (ex: Api, Api.External, Api.External.Fake)
        var rootNodes = new Dictionary<string, NamespaceNodeViewModel>();
        foreach (var ns in nsStrings.OrderBy(x => x))
        {
            var parts = ns.Split('.');
            NamespaceNodeViewModel? current = null;
            string currentPath = string.Empty;
            for (int i = 0; i < parts.Length; i++)
            {
                currentPath = i == 0 ? parts[0] : $"{currentPath}.{parts[i]}";
                if (i == 0)
                {
                    if (!rootNodes.TryGetValue(parts[0], out var root))
                    {
                        root = new NamespaceNodeViewModel(parts[0]);
                        rootNodes[parts[0]] = root;
                    }

                    current = root;
                }
                else if (current is not null)
                {
                    var child = current.Children.FirstOrDefault(c => c.Name == currentPath);
                    if (child is null)
                    {
                        child = new NamespaceNodeViewModel(currentPath) { Parent = current };
                        current.Children.Add(child);
                    }

                    current = child;
                }
            }
        }

        return rootNodes.Values.ToList();
    }

    public ProjectModel() => NamespaceTree = [];

    public void BuildNamespaceTree()
    {
        NamespaceTree.Clear();
        foreach (var node in GetNamespaceTree())
        {
            NamespaceTree.Add(node);
        }

        Source = new HierarchicalTreeDataGridSource<NamespaceNodeViewModel>(NamespaceTree)
        {
            Columns =
            {
                new CheckBoxColumn<NamespaceNodeViewModel>("Selected", x => x.IsChecked,
                    (x, value) => x.IsChecked = value),
                new HierarchicalExpanderColumn<NamespaceNodeViewModel>(
                    new TextColumn<NamespaceNodeViewModel, string>
                        ("Namespace", x => x.Name), x => x.Children)
            },
        };
    }

    /// <summary>
    /// Retorna a lista de namespaces selecionados (apenas maiores pais, sem filhos redundantes).
    /// </summary>
    public List<string> GetSelectedNamespaces()
    {
        var result = new List<string>();
        foreach (var node in NamespaceTree)
        {
            GetSelectedRecursive(node, result);
        }
        return result;
    }

    private static void GetSelectedRecursive(NamespaceNodeViewModel node, List<string> result)
    {
        // Se toda a subárvore estiver selecionada, adiciona apenas o pai
        if (AllDescendantsChecked(node))
        {
            result.Add(node.Name);
            return;
        }

        // Caso contrário, desce e coleta apenas os sub-ramos necessários
        foreach (var child in node.Children)
        {
            GetSelectedRecursive(child, result);
        }
    }

    private static bool AllDescendantsChecked(NamespaceNodeViewModel node)
    {
        if (!node.IsChecked)
        {
            return false;
        }

        foreach (var child in node.Children)
        {
            if (!AllDescendantsChecked(child))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Retorna a lista de namespaces não selecionados (apenas maiores pais, sem filhos redundantes).
    /// </summary>
    public List<string> GetUnselectedNamespaces()
    {
        var result = new List<string>();
        foreach (var node in NamespaceTree)
        {
            GetUnselectedRecursive(node, result);
        }
        return result;
    }

    private static void GetUnselectedRecursive(NamespaceNodeViewModel node, List<string> result)
    {
        // Se toda a subárvore estiver desmarcada, adiciona apenas o pai
        if (AllDescendantsUnchecked(node))
        {
            result.Add(node.Name);
            return;
        }

        // Caso contrário, desce e coleta apenas os sub-ramos necessários
        foreach (var child in node.Children)
        {
            GetUnselectedRecursive(child, result);
        }
    }

    private static bool AllDescendantsUnchecked(NamespaceNodeViewModel node)
    {
        if (node.IsChecked)
        {
            return false;
        }

        foreach (var child in node.Children)
        {
            if (!AllDescendantsUnchecked(child))
            {
                return false;
            }
        }

        return true;
    }
}
