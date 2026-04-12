using BrrainzBot.Infrastructure;

namespace BrrainzBot.Tests;

public sealed class SelfUpdateServiceTests
{
    [Fact]
    public void AssetSelectionReturnsNullWhenNothingMatches()
    {
        var asset = SelfUpdateService.SelectAsset(
        [
            new GitHubReleaseAsset("brrainzbot-docs.zip", "https://example.invalid/a", 1),
            new GitHubReleaseAsset("brrainzbot-source.zip", "https://example.invalid/b", 1)
        ]);

        Assert.Null(asset);
    }
}
