using CollectorUI.Models;

namespace CollectorUI.Services;

public interface IReportGeneratorService
{
    Task<string> CreateReportAsync(string? solutionPath, List<ProjectModel> testProjects,
        CancellationToken cancellationToken = default);
}