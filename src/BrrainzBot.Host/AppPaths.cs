using System.Runtime.InteropServices;

namespace BrrainzBot.Host;

public sealed record AppPaths(
    string RootDirectory,
    string ConfigFilePath,
    string SecretsFilePath,
    string SessionStateFilePath,
    string LogsDirectory,
    string DownloadsDirectory)
{
    public static AppPaths CreateDefault()
    {
        var root = ResolveDefaultRoot();
        return new AppPaths(
            root,
            Path.Combine(root, "config.json"),
            Path.Combine(root, "secrets.json"),
            Path.Combine(root, "state", "sessions.json"),
            Path.Combine(root, "logs"),
            Path.Combine(root, "downloads"));
    }

    public static AppPaths FromRoot(string rootDirectory)
    {
        var fullRoot = Path.GetFullPath(Environment.ExpandEnvironmentVariables(rootDirectory));
        return new AppPaths(
            fullRoot,
            Path.Combine(fullRoot, "config.json"),
            Path.Combine(fullRoot, "secrets.json"),
            Path.Combine(fullRoot, "state", "sessions.json"),
            Path.Combine(fullRoot, "logs"),
            Path.Combine(fullRoot, "downloads"));
    }

    public void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(SessionStateFilePath)!);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(DownloadsDirectory);
    }

    private static string ResolveDefaultRoot()
    {
        var baseDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            baseDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        var folderName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "BrrainzBot" : ".brrainzbot";
        return Path.Combine(baseDirectory, folderName);
    }
}
