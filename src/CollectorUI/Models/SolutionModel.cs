using System.Xml.Linq;

namespace CollectorUI.Models;

public class SolutionModel
{
    public string? SolutionPath { get; set; }
    public List<ProjectModel> Projects { get; set; } = [];
    public List<ProjectModel> TestProjects => Projects.Where(p => p.IsTestProject).ToList();

    public static SolutionModel ParseFromFile(string filePath)
    {
        var solution = new SolutionModel
        {
            SolutionPath = filePath
        };

        var solutionDir = Path.GetDirectoryName(filePath);
        var ext = Path.GetExtension(filePath)?.ToLowerInvariant();

        var projectPaths =
            ext == ".slnx" ? GetProjectPathsFromSlnx(filePath,solutionDir) :
            ext == ".sln" ? GetProjectPathsFromSln(filePath,solutionDir) :
            [];

        foreach (var projectPath in projectPaths)
        {
            if (File.Exists(projectPath))
            {
                var project = ProjectModel.FromProjectFile(projectPath);
                solution.Projects.Add(project);
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

    private static IEnumerable<string> GetProjectPathsFromSlnx(string filePath, string? solutionDir)
    {
        if (solutionDir is null)
        {
            yield break;
        }

        var baseDir = Path.GetFullPath(solutionDir);
        var doc = XDocument.Load(filePath);

        foreach (var projectElement in doc.Descendants("Project"))
        {
            var projectPathAttr = projectElement.Attribute("Path");
            if (projectPathAttr != null)
            {
                string rawPath = projectPathAttr.Value;
                rawPath = rawPath
                    .Replace('\\', Path.DirectorySeparatorChar)
                    .Replace('/', Path.DirectorySeparatorChar);
                var projectPath = Path.GetFullPath(rawPath, baseDir);
                yield return projectPath;
            }
        }
    }

   private static IEnumerable<string> GetProjectPathsFromSln(string filePath, string? solutionDir)
    {
        if (solutionDir is null)
        {
            yield break;
        }

        var baseDir = Path.GetFullPath(solutionDir);
        const string pattern = @"^\s*Project\(.*\)\s=\s""[^""]+"",\s*""([^""]+\.(?:csproj|vbproj|fsproj))"",\s*""\{[^""]+}""";

        foreach (var line in File.ReadLines(filePath))
        {
            var match = System.Text.RegularExpressions.Regex.Match(line, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var relativePath = match.Groups[1].Value
                    .Replace('\\', Path.DirectorySeparatorChar)
                    .Replace('/', Path.DirectorySeparatorChar);
                var fullPath = Path.GetFullPath(relativePath, baseDir);
                yield return fullPath;
            }
        }
    }
}
