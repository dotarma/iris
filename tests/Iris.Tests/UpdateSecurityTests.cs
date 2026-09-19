using Iris.Core;
using Iris.Services;

namespace Iris.Tests;

public sealed class UpdateSecurityTests
{
    [Fact]
    public void Metadata_AcceptsKnownArchitecturesAndTrustedPackages()
    {
        var metadata = ValidMetadata();
        UpdateService.ValidateMetadata(metadata);
    }

    [Theory]
    [InlineData("http://github.com/dotarma/iris/releases/download/v1/Iris.exe")]
    [InlineData("https://evil.example/Iris.exe")]
    [InlineData("https://github.com/dotarma/iris/releases/download/v1/Iris.txt")]
    public void Metadata_RejectsUntrustedPackage(string url)
    {
        var metadata = ValidMetadata(new Uri(url));
        Assert.Throws<InvalidDataException>(() => UpdateService.ValidateMetadata(metadata));
    }

    [Fact]
    public void Metadata_RejectsMalformedChecksum()
    {
        var metadata = ValidMetadata();
        metadata.Architectures["x64"] = new UpdatePackage
        {
            Url = new Uri("https://github.com/dotarma/iris/releases/download/v1.1.0/Iris-1.1.0-x64.zip"),
            Sha256 = "not-a-hash"
        };
        Assert.Throws<InvalidDataException>(() => UpdateService.ValidateMetadata(metadata));
    }

    [Fact]
    public void Metadata_RejectsWrongRepositoryPath()
    {
        var metadata = ValidMetadata(new Uri("https://github.com/attacker/evil/releases/download/v1.1.0/Iris-1.1.0-x64.zip"));
        Assert.Throws<InvalidDataException>(() => UpdateService.ValidateMetadata(metadata));
    }

    private static UpdateMetadata ValidMetadata(Uri? url = null) => new()
    {
        Version = "1.1.0",
        MinimumVersion = "1.0.0",
        Channel = "update",
        ReleaseDate = DateOnly.FromDateTime(DateTime.UtcNow),
        ReleaseNotes = "Security and reliability update.",
        Architectures = new Dictionary<string, UpdatePackage>
        {
            ["x64"] = new()
            {
                Url = url ?? new Uri("https://github.com/dotarma/iris/releases/download/v1.1.0/Iris-1.1.0-x64.zip"),
                Sha256 = new string('a', 64)
            }
        }
    };
}
