using CollectorUI.Data;
using Microsoft.EntityFrameworkCore;

namespace CollectorUI.Services;

public static class SelectionService
{
    static SelectionService() => AppDbContext.EnsureCreated();

    // Carrega namespaces desmarcados (IsChecked == false) para um projeto específico numa solução
    public static HashSet<string> LoadDeselectedNamespaces(string solutionPath, string projectPath)
    {
        using var ctx = new AppDbContext();
        var items = ctx.NamespaceSelections
            .AsNoTracking()
            .Where(s => s.SolutionPath == solutionPath && s.ProjectPath == projectPath && s.IsChecked == false)
            .Select(s => s.Namespace)
            .ToList();

        return new HashSet<string>(items, StringComparer.Ordinal);
    }

    // Substitui os registos de uma solução pelos estados desmarcados atuais
    public static void SaveDeselectedForSolution(string solutionPath, IEnumerable<(string ProjectPath, IEnumerable<string> DeselectedNamespaces)> data)
    {
        using var ctx = new AppDbContext();
        using var tx = ctx.Database.BeginTransaction();

        // Apaga registos anteriores da solução
        var old = ctx.NamespaceSelections.Where(s => s.SolutionPath == solutionPath);
        ctx.NamespaceSelections.RemoveRange(old);
        ctx.SaveChanges();

        // Insere novos registos (apenas desmarcados)
        var now = DateTime.UtcNow;
        var toAdd = new List<NamespaceSelection>();
        foreach (var (projectPath, deselected) in data)
        {
            foreach (var ns in deselected.Distinct(StringComparer.Ordinal))
            {
                toAdd.Add(new NamespaceSelection
                {
                    SolutionPath = solutionPath,
                    ProjectPath = projectPath,
                    Namespace = ns,
                    IsChecked = false,
                    SavedAt = now
                });
            }
        }

        if (toAdd.Count > 0)
        {
            ctx.NamespaceSelections.AddRange(toAdd);
            ctx.SaveChanges();
        }

        tx.Commit();
    }
}
