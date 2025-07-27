using CollectorUI.Models;

namespace CollectorUI.Services;

public class SolutionParserService
{
    public static async Task<SolutionModel> ParseSolutionAsync(string solutionPath) => await Task.Run(() => SolutionModel.ParseFromFile(solutionPath));

    public static bool IsSolutionFile(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return false;
        }

        var extension = Path.GetExtension(filePath);
        return extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase);
    }
}
