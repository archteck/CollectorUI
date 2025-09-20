using System.Xml.Linq;
using CollectorUI.ViewModels;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using CollectorUI.Services;

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

            // Check for test-related package references
            if (ns != null)
            {
                var packageRefs = doc.Descendants(ns + "PackageReference");
                var hasCoverlet = false;
                var hasTestFramework = false;
                foreach (var packageRef in packageRefs)
                {
                    var packageName = packageRef.Attribute("Include")?.Value;
                    if (packageName != null)
                    {
                        if (packageName.Contains("coverlet.msbuild"))
                        {
                            hasCoverlet = true;
                        }
                        if (packageName.Contains("xunit") ||
                            packageName.Contains("NUnit") ||
                            packageName.Contains("MSTest"))
                        {
                            hasTestFramework = true;
                        }
                        if(hasCoverlet && hasTestFramework)
                        {
                            project.IsTestProject = true;
                            break;
                        }
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

    // Mantém a árvore completa (todas as raízes) para realizar filtragem/clonagem.
    private List<NamespaceNodeViewModel> _allNamespaceRoots = new();

    // Texto do filtro (atualiza a exibição ao mudar).
    private string? _filterText;
    public string? FilterText
    {
        get => _filterText;
        set
        {
            if (_filterText == value)
            {
                return;
            }
            _filterText = value;
            ApplyFilter();
        }
    }

    // Aplica o filtro reconstruindo a árvore visível a partir de _allNamespaceRoots,
    // preservando os estados de seleção atuais.
    private void ApplyFilter()
    {
        var selectionMap = CaptureSelectionStates();
        var prevExpansion = CaptureExpansionStates();
        RebuildVisibleTree(selectionMap,prevExpansion);

        // Recria a source para garantir que o estado IsExpanded é respeitado após filtro.
        RecreateSource();
    }

    // Captura o estado de seleção atual por nome de namespace.
    private Dictionary<string, bool> CaptureSelectionStates()
    {
        var map = new Dictionary<string, bool>(StringComparer.Ordinal);
        void Visit(NamespaceNodeViewModel n)
        {
            map[n.Name] = n.IsChecked;
            foreach (var c in n.Children)
            {
                Visit(c);
            }
        }
        foreach (var r in NamespaceTree)
        {
            Visit(r);
        }

        return map;
    }

    // Captura o estado de expansão atual por nome de namespace.
    private Dictionary<string, bool> CaptureExpansionStates()
    {
        var map = new Dictionary<string, bool>(StringComparer.Ordinal);
        void Visit(NamespaceNodeViewModel n)
        {
            map[n.Name] = n.IsExpanded;
            foreach (var c in n.Children)
            {
                Visit(c);
            }
        }
        foreach (var r in NamespaceTree)
        {
            Visit(r);
        }

        return map;
    }

    // Reconstrói NamespaceTree a partir de _allNamespaceRoots, aplicando o filtro e estados de seleção/expansão.
    private void RebuildVisibleTree(Dictionary<string, bool> selectionMap, Dictionary<string, bool> expansionMap)
    {
        var term = string.IsNullOrWhiteSpace(_filterText) ? null : _filterText!.Trim();

        NamespaceTree.Clear();

        if (_allNamespaceRoots is null || _allNamespaceRoots.Count == 0)
        {
            return;
        }

        foreach (var root in _allNamespaceRoots)
        {
            var clone = CloneFiltered(root, term, selectionMap, expansionMap);
            if (clone is not null)
            {
                // Expande todos quando há filtro para facilitar a visualização.
                if (term is not null)
                {
                    ExpandRecursive(clone);
                }
                NamespaceTree.Add(clone);
            }
        }
    }

    // Clona o nó aplicando o filtro; retorna null se nem o nó nem os descendentes combinarem.
    private static NamespaceNodeViewModel? CloneFiltered(
        NamespaceNodeViewModel node,
        string? term,
        Dictionary<string, bool> selectionMap,
        Dictionary<string, bool> expansionMap)
    {
        bool selfMatches = term is null || node.Name.Contains(term, StringComparison.OrdinalIgnoreCase);

        var filteredChildren = new List<NamespaceNodeViewModel>();
        foreach (var child in node.Children)
        {
            var childClone = CloneFiltered(child, term, selectionMap, expansionMap);
            if (childClone is not null)
            {
                filteredChildren.Add(childClone);
            }
        }

        if (!selfMatches && filteredChildren.Count == 0)
        {
            return null;
        }

        var clone = new NamespaceNodeViewModel(node.Name)
        {
            IsChecked = selectionMap.TryGetValue(node.Name, out var isChecked) ? isChecked : true,
            IsExpanded = expansionMap.TryGetValue(node.Name, out var isExp) ? isExp : node.IsExpanded
        };

        foreach (var fc in filteredChildren)
        {
            fc.Parent = clone;
            clone.Children.Add(fc);
        }

        return clone;
    }

    private static void ExpandRecursive(NamespaceNodeViewModel node)
    {
        node.IsExpanded = true;
        foreach (var child in node.Children)
        {
            ExpandRecursive(child);
        }
    }

    /// <summary>
    /// Aplica estados desmarcados guardados em BD para esta solução/projeto (default permanece true).
    /// </summary>
    /// <param name="solutionPath">Caminho completo da solução.</param>
    public void ApplyDeselectionStates(string solutionPath)
    {
        if (string.IsNullOrWhiteSpace(solutionPath) || string.IsNullOrWhiteSpace(FullPath))
        {
            return;
        }

        var deselected = SelectionService.LoadDeselectedNamespaces(solutionPath, FullPath!);
        if (deselected.Count == 0)
        {
            return;
        }

        void Visit(NamespaceNodeViewModel n)
        {
            if (deselected.Contains(n.Name))
            {
                n.IsChecked = false;
            }
            foreach (var c in n.Children)
            {
                Visit(c);
            }
        }

        foreach (var root in NamespaceTree)
        {
            Visit(root);
        }
    }

    /// <summary>
    /// Enumera todos os namespaces atualmente desmarcados.
    /// </summary>
    public IEnumerable<string> GetDeselectedNamespaces()
    {
        IEnumerable<string> Visit(NamespaceNodeViewModel n)
        {
            if (!n.IsChecked)
            {
                yield return n.Name;
            }

            foreach (var c in n.Children)
            {
                foreach (var x in Visit(c))
                {
                    yield return x;
                }
            }
        }

        foreach (var root in NamespaceTree)
        {
            foreach (var x in Visit(root))
            {
                yield return x;
            }
        }
    }

    public void BuildNamespaceTree()
    {
        // Preserva seleção e expansão atuais antes de reconstruir tudo.
        var prevSelection = CaptureSelectionStates();
        var prevExpansion = CaptureExpansionStates();

        // Reconstroi a árvore completa a partir do modelo.
        _allNamespaceRoots = GetNamespaceTree();

        // Atualiza a coleção visível conforme o filtro atual.
        NamespaceTree.Clear();
        RebuildVisibleTree(prevSelection, prevExpansion);

        // (Re)cria a fonte (Source) – ligar expansão ao IsExpanded.
        RecreateSource();
    }

    // Helper para recriar a fonte com colunas e binding de expansão.
    private void RecreateSource() =>
        Source = new HierarchicalTreeDataGridSource<NamespaceNodeViewModel>(NamespaceTree)
        {
            Columns =
            {
                new CheckBoxColumn<NamespaceNodeViewModel>("Selected", x => x.IsChecked,
                    (x, value) => x.IsChecked = value),
                new HierarchicalExpanderColumn<NamespaceNodeViewModel>(
                    new TextColumn<NamespaceNodeViewModel, string>
                        ("Namespace", x => x.Name),
                    x => x.Children,
                    isExpandedSelector: x => x.IsExpanded)
            },
        };

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
