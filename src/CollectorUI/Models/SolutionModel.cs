using System.Xml.Linq;

namespace CollectorUI.Models;

public class SolutionModel
{
    public string? SolutionPath { get; set; }
    public List<ProjectModel> Projects { get; set; } = new List<ProjectModel>();
    public List<ProjectModel> TestProjects => Projects.Where(p => p.IsTestProject).ToList();

    public static SolutionModel ParseFromFile(string filePath)
    {
        var solution = new SolutionModel
        {
            SolutionPath = filePath
        };

        var solutionDir = Path.GetDirectoryName(filePath);
        var doc = XDocument.Load(filePath);

        foreach (var projectElement in doc.Descendants("Project"))
        {
            var projectPathAttr = projectElement.Attribute("Path");
            if (projectPathAttr != null)
            {
                if (solutionDir != null)
                {
                    var baseDir = Path.GetFullPath(solutionDir);
                    var projectPath = Path.GetFullPath(projectPathAttr.Value, baseDir);
                    if (File.Exists(projectPath))
                    {
                        var project = ProjectModel.FromProjectFile(projectPath);
                        solution.Projects.Add(project);
                    }
                }
            }
        }

        // Find project dependencies
        foreach (var project in solution.Projects)
        {
            project.FindDependencies(solution.Projects);
        }

        // Rebuild namespace trees now that dependencies are known
        foreach (var project in solution.Projects)
        {
            project.BuildNamespaceTree();
        }

        return solution;
    }
}
