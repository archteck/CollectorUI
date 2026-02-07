using System.Collections.Generic;
using System.Threading.Tasks;
using CollectorUI.Models;

namespace CollectorUI.Services;

public interface IReportGeneratorService
{
    Task<string> CreateReportAsync(string? solutionPath, List<ProjectModel> testProjects);
}