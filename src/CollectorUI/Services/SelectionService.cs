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

    // Regista/atualiza a solução para a qual se gerou report
    public static void UpsertSolutionReport(string solutionPath)
    {
        using var ctx = new AppDbContext();
        var existing = ctx.SolutionReports.FirstOrDefault(r => r.SolutionPath == solutionPath);
        var now = DateTime.UtcNow;

        if (existing is null)
        {
            ctx.SolutionReports.Add(new SolutionReport
            {
                SolutionPath = solutionPath,
                LastGeneratedAt = now
            });
        }
        else
        {
            existing.LastGeneratedAt = now;
            ctx.SolutionReports.Update(existing);
        }

        ctx.SaveChanges();
    }

    // Obtém lista de soluções recentes baseadas nos reports gerados
    public static IReadOnlyList<string> GetRecentSolutions(int limit = 10)
    {
        using var ctx = new AppDbContext();
        var recent = ctx.SolutionReports
            .AsNoTracking()
            .OrderByDescending(x => x.LastGeneratedAt)
            .Take(limit)
            .Select(x => x.SolutionPath)
            .ToList();

        return recent;
    }

    // Remove todos os registos associados a uma solução
    public static void RemoveSolutionRecords(string solutionPath)
    {
        using var ctx = new AppDbContext();

        var nsItems = ctx.NamespaceSelections.Where(s => s.SolutionPath == solutionPath);
        ctx.NamespaceSelections.RemoveRange(nsItems);

        var repItems = ctx.SolutionReports.Where(s => s.SolutionPath == solutionPath);
        ctx.SolutionReports.RemoveRange(repItems);

        ctx.SaveChanges();
    }
}
