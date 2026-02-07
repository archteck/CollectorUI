using System;
using System.Threading;
using System.Threading.Tasks;

namespace CollectorUI.Services;

public class UpdateServiceWrapper : IUpdateService
{
    public Task<UpdateService.ReleaseInfo?> GetLatestReleaseAsync(CancellationToken ct = default) => UpdateService.GetLatestReleaseAsync(ct);

    public bool IsUpdateAvailable(Version latest) => UpdateService.IsUpdateAvailable(latest);

    public Task<string> DownloadAssetAsync(string url, IProgress<double>? progress = null, CancellationToken ct = default) => UpdateService.DownloadAssetAsync(url, progress, ct);

    public string ExtractToNewFolder(string zipPath, string? versionLabel = null) => UpdateService.ExtractToNewFolder(zipPath, versionLabel);

    public bool StartUpdaterAndExit(string extractedDir) => UpdateService.StartUpdaterAndExit(extractedDir);
}