using System;
using System.Threading;
using System.Threading.Tasks;

namespace CollectorUI.Services;

public interface IUpdateService
{
    Task<UpdateService.ReleaseInfo?> GetLatestReleaseAsync(CancellationToken ct = default);

    bool IsUpdateAvailable(Version latest);

    Task<string> DownloadAssetAsync(string url, IProgress<double>? progress = null, CancellationToken ct = default);

    string ExtractToNewFolder(string zipPath, string? versionLabel = null);

    bool StartUpdaterAndExit(string extractedDir);
}