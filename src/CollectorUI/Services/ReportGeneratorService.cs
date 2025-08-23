using System.Diagnostics;
using CollectorUI.Models;

namespace CollectorUI.Services;

public static class ReportGeneratorService
{
    public static async Task<string> CreateReportAsync(string? solutionPath, List<ProjectModel> testProjects)
    {
        //Validate path
        if (!Path.Exists(solutionPath) || Path.GetExtension(solutionPath) != ".slnx")
        {
            return "Invalid Solution Path";
        }

        //build da slnx
        var buildResult = await BuildSolutionAsync(solutionPath);
        if (!buildResult.StartsWith("Build succeeded", StringComparison.OrdinalIgnoreCase))
        {
            return "Build failed";
        }

        foreach (var testProject in testProjects.Where(p => p is { IsTestProject: true, IsSelected: true }))
        {
            //include e exclude do que é para in/exc
            var includeList = testProject.GetSelectedNamespaces();
            var excludeList = testProject.GetUnselectedNamespaces();
            string include = includeList.Aggregate("", (current, includeListItem) => current + $"[*]{includeListItem}.*,");
            if (!string.IsNullOrEmpty(include))
            {
                include = include.Substring(0, include.Length - 1);
            }
            string exclude = excludeList.Aggregate("", (current, excludeListItem) => current + $"[*]{excludeListItem}.*,");
            if (!string.IsNullOrEmpty(exclude))
            {
                exclude = exclude.Substring(0, exclude.Length - 1);
            }
            //criar cobertura para cada projecto
           await CreateCoberturaAsync(testProject.FullPath!, include, exclude);
        }

        await Task.Delay(1);
        return "Success";
    }

    private static async Task<string> BuildSolutionAsync(string solutionPath)
    {
        // Setup the process start info
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"build \"{solutionPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            using var process = Process.Start(psi);
            if (process == null)
            {
                return "Failed to start build process.";
            }

            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            return process.ExitCode == 0
                ? $"Build succeeded:\n{output}"
                : $"Build failed:\n{error}";
        }
        catch (Exception ex)
        {
            return $"Exception during build: {ex.Message}";
        }
    }
    private static async Task<string> CreateCoberturaAsync(string projectPath, string include, string exclude)
    {
        var arg =
            $"test \"{projectPath}\" --no-build -p:TestingPlatformDotnetTestSupport=false -p:UseMicrosoftTestingPlatformRunner=false /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:CoverletOutput=./TestResults/";

        if (!string.IsNullOrWhiteSpace(include))
        {
            arg += $" -p:Include=\"{include}\"";
        }
        if (!string.IsNullOrWhiteSpace(exclude))
        {
            arg += $" -p:Exclude=\"{exclude}\"";
        }
        // Setup the process start info
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = arg,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            using var process = Process.Start(psi);
            if (process == null)
            {
                return "Failed to start build process.";
            }

            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            return process.ExitCode == 0
                ? $"Build succeeded:\n{output}"
                : $"Build failed:\n{error}";
        }
        catch (Exception ex)
        {
            return $"Exception during build: {ex.Message}";
        }
    }

    private static async Task<string> RunCommand(string file, params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = file,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }


        var outputBuilder = new System.Text.StringBuilder();
        var errorBuilder = new System.Text.StringBuilder();

        using (var proc = Process.Start(psi)!)
        {
            // Asynchronously read standard output and standard error
            var outputTask = Task.Run(() =>
            {
                using var reader = proc.StandardOutput; // Use the captured proc here
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    if (line != null)
                    {
                        outputBuilder.AppendLine(line);
                    }
                }
            });

            var errorTask = Task.Run(() =>
            {
                using var reader = proc.StandardError; // Use the captured proc here
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    if (line != null)
                    {
                        errorBuilder.AppendLine(line);
                    }
                }
            });

            await Task.WhenAll(outputTask, errorTask, proc.WaitForExitAsync());
        }

        var stdout = outputBuilder.ToString();
        var stderr = errorBuilder.ToString();
        return string.IsNullOrWhiteSpace(stderr)
            ? stdout
            : $"{stdout}\nERROR:\n{stderr}";
    }
}
