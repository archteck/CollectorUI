using System.Diagnostics;
using CollectorUI.Models;
using System.Text.RegularExpressions;
using Serilog;

namespace CollectorUI.Services;

public static partial class ReportGeneratorService
{
    public static async Task<string> CreateReportAsync(string? solutionPath, List<ProjectModel> testProjects)
    {
        // Garantir que o reportgenerator está instalado
        var ensured = await EnsureReportGeneratorInstalledAsync();
        if (!ensured.success)
        {
            Log.Error("Failed to ensure reportgenerator tool: {Message}", ensured.message);
            return $"Failed to ensure reportgenerator: {ensured.message}";
        }
        Log.Information("CreateReportAsync started for {SolutionPath} with {ProjectCount} test project(s)",
            solutionPath, testProjects.Count);

        //Validate path
        if (!Path.Exists(solutionPath) || Path.GetExtension(solutionPath) != ".slnx")
        {
            Log.Warning("Invalid Solution Path provided: {SolutionPath}", solutionPath);
            return "Invalid Solution Path";
        }

        Log.Information("Building solution {SolutionPath}", solutionPath);
        //build da slnx
        var buildResult = await BuildSolutionAsync(solutionPath);
        Log.Debug("Build result for {SolutionPath}: {BuildResult}", solutionPath, buildResult);
        if (!buildResult.StartsWith("Build succeeded", StringComparison.OrdinalIgnoreCase))
        {
            Log.Error("Build failed for {SolutionPath}", solutionPath);
            // Preserve detailed build output to surface the failure reason in the UI
            return buildResult;
        }

        var selectedProjects = testProjects.Where(p => p is { IsTestProject: true, IsSelected: true }).ToList();
        if (selectedProjects.Count == 0)
        {
            Log.Warning("No test projects selected for {SolutionPath}", solutionPath);
            return "No test projects selected.";
        }

        Log.Information("Selected {SelectedCount} test project(s) out of {TotalCount}", selectedProjects.Count,
            testProjects!.Count);

        int successCount = 0;
        var failures = new List<string>();

        foreach (var testProject in selectedProjects)
        {
            var projectName = string.IsNullOrWhiteSpace(testProject.Name) ? testProject.FullPath : testProject.Name;

            //include e exclude do que é para in/exc
            var includeList = testProject.GetSelectedNamespaces();
            var excludeList = testProject.GetUnselectedNamespaces();
            string include =
                includeList.Aggregate("", (current, includeListItem) => current + $"[*]{includeListItem}.*,");
            if (!string.IsNullOrEmpty(include))
            {
                include = include[..^1];
            }

            string exclude =
                excludeList.Aggregate("", (current, excludeListItem) => current + $"[*]{excludeListItem}.*,");
            if (!string.IsNullOrEmpty(exclude))
            {
                exclude = exclude[..^1];
            }

            Log.Debug("Running coverage for project {Project} Include='{Include}' Exclude='{Exclude}'", projectName,
                include, exclude);

            //criar cobertura para cada projecto
            var (success, indexPath, message) =
                await CreateCoberturaAndReportAsync(testProject.FullPath!, include, exclude);
            if (success && !string.IsNullOrWhiteSpace(indexPath))
            {
                testProject.CoverageReportIndexPath = indexPath;
                successCount++;
                Log.Information("Coverage report generated for {Project}: {IndexPath}", projectName, indexPath);
            }
            else
            {
                failures.Add($"{projectName}: {message}");
                Log.Error("Coverage generation failed for {Project}: {Message}", projectName, message);
            }
        }

        await Task.Delay(1);

        if (successCount == 0)
        {
            var err = failures.FirstOrDefault() ?? "Unknown error";
            Log.Error("Failed to generate coverage reports. First/last error: {Error}", err);
            return $"Failed to generate coverage reports. {err}";
        }

        if (failures.Count > 0)
        {
            Log.Warning("Generated {Success}/{Total} coverage report(s). Some projects failed. Last error: {LastError}",
                successCount, selectedProjects.Count, failures[^1]);
            return
                $"Generated {successCount}/{selectedProjects.Count} coverage report(s). Some projects failed. Last error: {failures[^1]}";
        }

        Log.Information("Coverage reports generated successfully for all {Total} selected projects",
            selectedProjects.Count);
        return "Success";
    }

    private static async Task<string> BuildSolutionAsync(string solutionPath)
    {
        Log.Information("Starting dotnet build for {SolutionPath}", solutionPath);

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
                Log.Error("Failed to start build process for {SolutionPath}", solutionPath);
                return "Failed to start build process.";
            }

            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                Log.Information("Build succeeded for {SolutionPath}", solutionPath);
                Log.Debug("Build stdout: {Stdout}", output);
                return $"Build succeeded:\n{output}";
            }

            Log.Error("Build failed for {SolutionPath} with exit code {ExitCode}", solutionPath, process.ExitCode);
            Log.Debug("Build stderr: {Stderr}", error);
            return $"Build failed:\n{error}";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Exception during build for {SolutionPath}", solutionPath);
            return $"Exception during build: {ex.Message}";
        }
    }

    private static async Task<(bool success, string? indexPath, string message)> CreateCoberturaAndReportAsync(
        string projectPath, string include, string exclude)
    {
        Log.Information("Running dotnet test for project {ProjectPath}", projectPath);
        Log.Debug("Coverage filters for {ProjectPath} Include='{Include}' Exclude='{Exclude}'", projectPath, include,
            exclude);

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
                Log.Error("Failed to start test process for {ProjectPath}", projectPath);
                return (false, null, "Failed to start build process.");
            }

            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                Log.Error("dotnet test failed for {ProjectPath} with exit code {ExitCode}", projectPath,
                    process.ExitCode);
                Log.Debug("dotnet test stderr: {Stderr}", error);
                return (false, null, $"Build failed:\n{error}");
            }

            Log.Debug("dotnet test stdout for {ProjectPath}: {Stdout}", projectPath, output);

            // Encontrar o caminho completo para coverage.cobertura.xml no output
            if (TryExtractCoberturaPath(output, out var coberturaPath))
            {
                Log.Information("Found coverage file for {ProjectPath} at {CoberturaPath}", projectPath, coberturaPath);

                // Executar reportgenerator
                var folderPath = Path.GetDirectoryName(coberturaPath)!;
                var targetDir = Path.Combine(folderPath, "coveragereport");
                var historyDir = Path.Combine(folderPath, "historycoveragereport");

                Log.Information("Generating HTML report for {ProjectPath} to {TargetDir}", projectPath, targetDir);

                var rgOutput = await RunCommand(
                    "reportgenerator",
                    $"-reports:{coberturaPath}",
                    $"-targetdir:{targetDir}",
                    $"-historydir:{historyDir}",
                    "-reporttypes:Html"
                );

                Log.Debug("reportgenerator output for {ProjectPath}: {Output}", projectPath, rgOutput);

                var indexPath = Path.Combine(targetDir, "index.html");
                Log.Information("Report generated for {ProjectPath}: {IndexPath}", projectPath, indexPath);
                return (true, indexPath, rgOutput);
            }

            Log.Warning("Coverage path not found in output for {ProjectPath}", projectPath);
            // Se não encontrarmos o caminho, devolvemos o output original como mensagem
            return (false, null, $"Build succeeded but coverage path not found:\n{output}");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Exception during coverage/report generation for {ProjectPath}", projectPath);
            return (false, null, $"Exception during build: {ex.Message}");
        }
    }

    private static async Task<string> RunCommand(string file, params string[] args)
    {
        Log.Debug("Executing command: {File} {Args}", file, string.Join(" ", args));

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
            Log.Debug("Command exited with code {ExitCode}", proc.ExitCode);
        }

        var stdout = outputBuilder.ToString();
        var stderr = errorBuilder.ToString();

        if (!string.IsNullOrWhiteSpace(stderr))
        {
            Log.Warning("Command produced stderr: {Stderr}", stderr);
        }

        Log.Debug("Command stdout: {Stdout}", stdout);

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
        var dirHint = MyRegex().Matches(output)
            .Cast<Match>()
            .Select(m => m.Value)
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(dirHint))
        {
            var possible =
                Path.Combine(
                    dirHint.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar),
                    "coverage.cobertura.xml");
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

    [GeneratedRegex(@"(TestResults[\\/][^\r\n]+)", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex MyRegex();
}
