using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace CollectorUI.Models;

public class ProjectModel
{
    public string? Name { get; set; }
    public string? FullPath { get; set; }
    public bool IsTestProject { get; set; }
    public List<string> ProjectReferences { get; set; } = new List<string>();
    public List<ProjectModel> Dependencies { get; set; } = new List<ProjectModel>();
    public List<NamespaceModel> Namespaces { get; set; } = new List<NamespaceModel>();
    public bool IsSelected { get; set; } = true;

    private static readonly char[] s_separator = new[] { '\r', '\n' };

    public static ProjectModel FromProjectFile(string projectPath)
    {
        var project = new ProjectModel
        {
            FullPath = projectPath,
            Name = Path.GetFileNameWithoutExtension(projectPath)
        };

        try
        {
            var doc = XDocument.Load(projectPath);
            var ns = doc.Root?.GetDefaultNamespace();

            // Check if it's a test project based on naming convention
            if (project.Name != null)
            {
                project.IsTestProject = project.Name.EndsWith(".Tests",  StringComparison.OrdinalIgnoreCase) ||
                                        project.Name.EndsWith(".Test",  StringComparison.OrdinalIgnoreCase) ||
                                        project.Name.EndsWith(".Testing",  StringComparison.OrdinalIgnoreCase) ||
                                        project.Name.Contains("Test");
            }

            // Also check for test-related package references
            if (ns != null)
            {
                var packageRefs = doc.Descendants(ns + "PackageReference");
                foreach (var packageRef in packageRefs)
                {
                    var packageName = packageRef.Attribute("Include")?.Value;
                    if (packageName != null &&
                        (packageName.Contains("xunit") ||
                         packageName.Contains("NUnit") ||
                         packageName.Contains("MSTest")))
                    {
                        project.IsTestProject = true;
                        break;
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
                        var fullPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(projectPath) ?? string.Empty, includePath));
                        project.ProjectReferences.Add(fullPath);
                    }
                }
            }

            // Parse namespaces from source files
            project.Namespaces = ExtractNamespaces(projectPath);
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

    private static List<NamespaceModel> ExtractNamespaces(string projectPath)
    {
        var namespaces = new HashSet<string>();
        var projectDir = Path.GetDirectoryName(projectPath);

        if (projectDir == null)
        {
            return new List<NamespaceModel>();
        }

        // Find all .cs files in the project directory and subdirectories
        foreach (var file in Directory.GetFiles(projectDir, "*.cs", SearchOption.AllDirectories))
        {
            try
            {
                var content = File.ReadAllText(file);
                var nsLine = content.Split(s_separator, StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault(line => line.TrimStart().StartsWith("namespace ",  StringComparison.OrdinalIgnoreCase));

                if (nsLine != null)
                {
                    var ns = nsLine.TrimStart().Substring(10).TrimEnd(';', ' ', '{');
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
}
