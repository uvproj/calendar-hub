namespace Calendar.Core.Tests;

public sealed class GoogleTokenCachePathTests
{
    [Fact]
    public void GetTokenCacheDirectory_UnsafeServiceIdentity_ReturnsDeterministicChildDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), $"google-token-root-{Guid.NewGuid():N}");

        var first = GoogleCalendarService.GetTokenCacheDirectory("../Family/Work", root);
        var second = GoogleCalendarService.GetTokenCacheDirectory("../family/work", root);

        Assert.Equal(first, second);
        Assert.Equal(Path.GetFullPath(root), Directory.GetParent(first)!.FullName);
    }
}