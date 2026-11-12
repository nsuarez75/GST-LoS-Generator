using GstLosGenerator.Core;

namespace GstLosGenerator.Tests;

public class UpdateCheckTests
{
    [Theory]
    [InlineData("v1.2.3", "1.2.3")]
    [InlineData("1.2", "1.2.0")]
    [InlineData("V2.0.1-beta", "2.0.1")]
    public void ParseVersion_Tag_ReturnsVersion(string tag, string expected) =>
        Assert.Equal(Version.Parse(expected), UpdateCheck.ParseVersion(tag));

    [Theory]
    [InlineData("")]
    [InlineData("ultima")]
    [InlineData("v1.x")]
    public void ParseVersion_NoVersion_ReturnsNull(string tag) => Assert.Null(UpdateCheck.ParseVersion(tag));

    [Fact]
    public void IsNewer_ReleaseAndAppVersion_Compares()
    {
        var release = UpdateCheck.Parse("""{"tag_name":"v0.2.0","html_url":"https://github.com/x/y/releases/tag/v0.2.0"}""")!;

        Assert.Equal("https://github.com/x/y/releases/tag/v0.2.0", release.Url);
        Assert.True(UpdateCheck.IsNewer(release, new Version(0, 1, 0, 0)));
        Assert.False(UpdateCheck.IsNewer(release, new Version(0, 2, 0, 0)));
        Assert.False(UpdateCheck.IsNewer(release, new Version(1, 0, 0)));
    }

    [Fact]
    public void Parse_InvalidResponse_ReturnsNull()
    {
        Assert.Null(UpdateCheck.Parse("{\"message\":\"Not Found\"}"));
        Assert.Null(UpdateCheck.Parse("no es json"));
    }
}
