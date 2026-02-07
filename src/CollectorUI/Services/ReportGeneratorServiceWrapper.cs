using System.Collections.Generic;
using System.Threading.Tasks;
using CollectorUI.Models;

namespace CollectorUI.Services;

public class ReportGeneratorServiceWrapper : IReportGeneratorService
{
    public Task<string> CreateReportAsync(string? solutionPath, List<ProjectModel> testProjects) => ReportGeneratorService.CreateReportAsync(solutionPath, testProjects);
}
