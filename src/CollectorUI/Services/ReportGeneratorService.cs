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
        // await BuildSolution(@"C:\Users\Teck\source\repos\TemplateWebApiCleanArchitecture\TemplateProject.slnx");
        var buildResult = await BuildSolution(solutionPath);
        if (!buildResult.StartsWith("Build succeeded", StringComparison.OrdinalIgnoreCase))
        {
            return "Build failed";
        }

        foreach (var testProject in testProjects.Where(p => p is { IsTestProject: true, IsSelected: true }))
        {
            //criar cobertura para cada projecto

            //include e exclude do que é para in/exc
        }

        await Task.Delay(1);
        return "Success";
    }

    private static async Task<string> BuildSolution(string solutionPath)
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
