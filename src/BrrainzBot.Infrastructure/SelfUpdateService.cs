using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Runtime.InteropServices;
using BrrainzBot.Host;

namespace BrrainzBot.Infrastructure;

public sealed class SelfUpdateService(
    GitHubReleaseService releaseService,
    IHttpClientFactory httpClientFactory,
    AppPaths paths)
{
    public async Task<PreparedSelfUpdate?> PrepareAsync(UpdateSettings updateSettings, CancellationToken cancellationToken)
    {
        var release = await releaseService.GetLatestAsync(updateSettings.Repository, cancellationToken);
        if (release == null)
            return null;

        var asset = SelectAsset(release.Assets);
        if (asset == null)
            return null;

        var downloadDirectory = Path.Combine(paths.DownloadsDirectory, release.TagName);
        Directory.CreateDirectory(downloadDirectory);

        var archivePath = Path.Combine(downloadDirectory, asset.Name);
        var extractedBinaryPath = Path.Combine(downloadDirectory, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "brrainzbot.exe" : "brrainzbot");

        using var httpClient = httpClientFactory.CreateClient(ServiceCollectionExtensions.GitHubHttpClientName);
        using var response = await httpClient.GetAsync(asset.DownloadUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using (var archiveStream = File.Create(archivePath))
        {
            await response.Content.CopyToAsync(archiveStream, cancellationToken);
        }

        await ExtractAsync(archivePath, downloadDirectory, cancellationToken);

        if (!File.Exists(extractedBinaryPath))
            throw new FileNotFoundException($"The downloaded archive did not contain {Path.GetFileName(extractedBinaryPath)}.");

        return new PreparedSelfUpdate(release, asset, archivePath, extractedBinaryPath);
    }

    public async Task ApplyAsync(string targetExecutablePath, string sourceExecutablePath, int waitForProcessId, CancellationToken cancellationToken)
    {
        if (waitForProcessId > 0)
        {
            try
            {
                using var process = Process.GetProcessById(waitForProcessId);
                await process.WaitForExitAsync(cancellationToken);
            }
            catch (ArgumentException)
            {
                // The process already exited.
            }
        }

        await Task.Delay(500, cancellationToken);

        var targetDirectory = Path.GetDirectoryName(targetExecutablePath) ?? throw new InvalidOperationException("Target path did not have a directory.");
        Directory.CreateDirectory(targetDirectory);

        if (File.Exists(targetExecutablePath))
            File.Delete(targetExecutablePath);

        File.Copy(sourceExecutablePath, targetExecutablePath, true);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(targetExecutablePath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute);
    }

    public string CreateHelperExecutable(string currentExecutablePath)
    {
        var helperPath = Path.Combine(paths.DownloadsDirectory, $"updater-{Guid.NewGuid():N}{Path.GetExtension(currentExecutablePath)}");
        Directory.CreateDirectory(paths.DownloadsDirectory);
        File.Copy(currentExecutablePath, helperPath, true);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(helperPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return helperPath;
    }

    public static GitHubReleaseAsset? SelectAsset(IEnumerable<GitHubReleaseAsset> assets)
    {
        var os = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win" :
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "osx" : "linux";
        var arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.Arm64 => "arm64",
            Architecture.X64 => "x64",
            Architecture.X86 => "x86",
            _ => RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant()
        };

        var rid = $"{os}-{arch}";
        return assets.FirstOrDefault(a => a.Name.Contains(rid, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task ExtractAsync(string archivePath, string destinationDirectory, CancellationToken cancellationToken)
    {
        if (archivePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            ZipFile.ExtractToDirectory(archivePath, destinationDirectory, overwriteFiles: true);
            return;
        }

        if (archivePath.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) || archivePath.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase))
        {
            await using var stream = File.OpenRead(archivePath);
            await using var gzip = new GZipStream(stream, CompressionMode.Decompress);
            TarFile.ExtractToDirectory(gzip, destinationDirectory, overwriteFiles: true);
            return;
        }

        throw new InvalidOperationException("Only .zip and .tar.gz release assets are supported.");
    }
}

public sealed record PreparedSelfUpdate(
    GitHubReleaseInfo Release,
    GitHubReleaseAsset Asset,
    string ArchivePath,
    string ExtractedBinaryPath);
