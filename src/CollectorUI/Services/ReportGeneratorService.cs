using System.Diagnostics;
using System.Threading.Tasks;

namespace CollectorUI.Services;

public class ReportGeneratorService
{

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

        using (  var proc = Process.Start(psi)!)
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
