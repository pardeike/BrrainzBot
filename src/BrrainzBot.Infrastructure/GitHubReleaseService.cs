using System.Net.Http.Headers;
using System.Text.Json;

namespace BrrainzBot.Infrastructure;

public sealed class GitHubReleaseService(IHttpClientFactory httpClientFactory)
{
    public async Task<GitHubReleaseInfo?> GetLatestAsync(string repository, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(ServiceCollectionExtensions.GitHubHttpClientName);
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("BrrainzBot", "0.1.1"));
        using var response = await client.GetAsync($"repos/{repository}/releases/latest", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;

        var assets = root.GetProperty("assets")
            .EnumerateArray()
            .Select(asset => new GitHubReleaseAsset(
                asset.GetProperty("name").GetString() ?? string.Empty,
                asset.GetProperty("browser_download_url").GetString() ?? string.Empty,
                asset.GetProperty("size").GetInt64()))
            .ToList();

        return new GitHubReleaseInfo(
            root.GetProperty("tag_name").GetString() ?? string.Empty,
            root.GetProperty("name").GetString() ?? string.Empty,
            root.GetProperty("body").GetString() ?? string.Empty,
            assets);
    }
}

public sealed record GitHubReleaseInfo(string TagName, string Name, string Body, IReadOnlyList<GitHubReleaseAsset> Assets);
public sealed record GitHubReleaseAsset(string Name, string DownloadUrl, long SizeBytes);
