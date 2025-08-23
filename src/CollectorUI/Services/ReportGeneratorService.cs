using System.Diagnostics;
using CollectorUI.Models;
using System.Text.RegularExpressions;

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
            // Preserve detailed build output to surface the failure reason in the UI
            return buildResult;
        }

        var selectedProjects = testProjects.Where(p => p is { IsTestProject: true, IsSelected: true }).ToList();
        if (selectedProjects.Count == 0)
        {
            return "No test projects selected.";
        }

        int successCount = 0;
        var failures = new List<string>();

        foreach (var testProject in selectedProjects)
        {
            //include e exclude do que é para in/exc
            var includeList = testProject.GetSelectedNamespaces();
            var excludeList = testProject.GetUnselectedNamespaces();
            string include = includeList.Aggregate("", (current, includeListItem) => current + $"[*]{includeListItem}.*,");
            if (!string.IsNullOrEmpty(include))
            {
                include = include[..^1];
            }
            string exclude = excludeList.Aggregate("", (current, excludeListItem) => current + $"[*]{excludeListItem}.*,");
            if (!string.IsNullOrEmpty(exclude))
            {
                exclude = exclude[..^1];
            }

            //criar cobertura para cada projecto
            var (success, indexPath, message) = await CreateCoberturaAndReportAsync(testProject.FullPath!, include, exclude);
            if (success && !string.IsNullOrWhiteSpace(indexPath))
            {
                testProject.CoverageReportIndexPath = indexPath;
                successCount++;
            }
            else
            {
                var projectName = string.IsNullOrWhiteSpace(testProject.Name) ? testProject.FullPath : testProject.Name;
                failures.Add($"{projectName}: {message}");
            }
        }

        await Task.Delay(1);

        if (successCount == 0)
        {
            var err = failures.FirstOrDefault() ?? "Unknown error";
            return $"Failed to generate coverage reports. {err}";
        }

        if (failures.Count > 0)
        {
            return $"Generated {successCount}/{selectedProjects.Count} coverage report(s). Some projects failed. Last error: {failures[^1]}";
        }

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
    private static async Task<(bool success, string? indexPath, string message)> CreateCoberturaAndReportAsync(string projectPath, string include, string exclude)
    {
        // Setup the process start info
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // dotnet test args
        psi.ArgumentList.Add("test");
        psi.ArgumentList.Add(projectPath);
        psi.ArgumentList.Add("--no-build");
        psi.ArgumentList.Add("-p:TestingPlatformDotnetTestSupport=false");
        psi.ArgumentList.Add("-p:UseMicrosoftTestingPlatformRunner=false");
        psi.ArgumentList.Add("/p:CollectCoverage=true");
        psi.ArgumentList.Add("/p:CoverletOutputFormat=cobertura");
        psi.ArgumentList.Add("/p:CoverletOutput=./TestResults/");

        if (!string.IsNullOrWhiteSpace(include))
        {
            psi.ArgumentList.Add($"/p:Include=\"{include}\"");
        }
        if (!string.IsNullOrWhiteSpace(exclude))
        {
            psi.ArgumentList.Add($"/p:Exclude=\"{exclude}\"");
        }

        try
        {
            using var process = Process.Start(psi);
            if (process == null)
            {
                return (false, null, "Failed to start build process.");
            }

            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                return (false, null, $"Build failed:\n{error}");
            }

            // Encontrar o caminho completo para coverage.cobertura.xml no output
            if (TryExtractCoberturaPath(output, out var coberturaPath))
            {
                // Garantir que o reportgenerator está instalado
                var ensured = await EnsureReportGeneratorInstalledAsync();
                if (!ensured.success)
                {
                    return (false, null, $"Failed to ensure reportgenerator: {ensured.message}");
                }

                // Executar reportgenerator
                var folderPath = Path.GetDirectoryName(coberturaPath)!;
                var targetDir = Path.Combine(folderPath, "coveragereport");

                var rgOutput = await RunCommand(
                    "reportgenerator",
                    $"-reports:{coberturaPath}",
                    $"-targetdir:{targetDir}",
                    "-reporttypes:Html"
                );

                var indexPath = Path.Combine(targetDir, "index.html");
                return (true, indexPath, rgOutput);
            }

            // Se não encontrarmos o caminho, devolvemos o output original como mensagem
            return (false, null, $"Build succeeded but coverage path not found:\n{output}");
        }
        catch (Exception ex)
        {
            return (false, null, $"Exception during build: {ex.Message}");
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

    private static bool TryExtractCoberturaPath(string output, out string path)
    {
        // Procura caminhos absolutos que terminem em coverage.cobertura.xml (Windows e Unix-like)
        var pattern = @"([A-Za-z]:\\[^\r\n]*?coverage\.cobertura\.xml)|(/[^:\r\n]*?/coverage\.cobertura\.xml)";
        var match = Regex.Matches(output, pattern, RegexOptions.IgnoreCase)
                         .Cast<Match>()
                         .FirstOrDefault(m => m.Success);

        if (match != null && match.Success)
        {
            path = match.Value.Trim();
            return true;
        }

        // Fallback: tentar compor o caminho esperado a partir do output da pasta de TestResults
        // Ex.: .../TestResults/<hash>/coverage.cobertura.xml
        var dirHint = Regex.Matches(output, @"(TestResults[\\/][^\r\n]+)", RegexOptions.IgnoreCase)
                           .Cast<Match>()
                           .Select(m => m.Value)
                           .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(dirHint))
        {
            var possible = Path.Combine(dirHint.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar), "coverage.cobertura.xml");
            if (File.Exists(possible))
            {
                path = Path.GetFullPath(possible);
                return true;
            }
        }

        path = string.Empty;
        return false;
    }

    private static async Task<(bool success, string message)> EnsureReportGeneratorInstalledAsync()
    {
        var listOut = await RunCommand("dotnet", "tool", "list", "-g");
        if (listOut.Contains("dotnet-reportgenerator-globaltool", StringComparison.OrdinalIgnoreCase))
        {
            return (true, "Already installed");
        }

        var installOut = await RunCommand("dotnet", "tool", "install", "-g", "dotnet-reportgenerator-globaltool");

        // Se a instalação falhar, o RunCommand já inclui stderr no retorno
        // Vamos validar instalação novamente para confirmar
        var postCheck = await RunCommand("dotnet", "tool", "list", "-g");
        var ok = postCheck.Contains("dotnet-reportgenerator-globaltool", StringComparison.OrdinalIgnoreCase);
        return (ok, ok ? "Installed" : installOut);
    }
}
