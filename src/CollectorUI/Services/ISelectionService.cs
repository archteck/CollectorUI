using System.Collections.Generic;

namespace CollectorUI.Services;

public interface ISelectionService
{
    HashSet<string> LoadDeselectedNamespaces(string solutionPath, string projectPath);

    void SaveDeselectedForSolution(string solutionPath, IEnumerable<(string ProjectPath, IEnumerable<string> DeselectedNamespaces)> data);

    void UpsertSolutionReport(string solutionPath);

    IReadOnlyList<string> GetRecentSolutions(int limit = 10);

    void RemoveSolutionRecords(string solutionPath);
}