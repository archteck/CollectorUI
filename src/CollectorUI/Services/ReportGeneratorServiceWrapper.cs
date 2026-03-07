using CollectorUI.Models;

namespace CollectorUI.Services;

public class ReportGeneratorServiceWrapper : IReportGeneratorService
{
    public Task<string> CreateReportAsync(string? solutionPath, List<ProjectModel> testProjects,
        CancellationToken cancellationToken = default)
        => ReportGeneratorService.CreateReportAsync(solutionPath, testProjects, cancellationToken);
}
