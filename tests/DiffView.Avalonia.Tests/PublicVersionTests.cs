using System.Text.Json;

namespace Bennewitz.Ninja.DiffView.Tests;

/// <summary>
/// What <c>Bennewitz.Ninja.AutoVersioning</c> records as a build's public version is that build's version. The
/// SDK gives <c>Version</c> its default after the props files, so a <c>PublicVersion</c> set from it in
/// <c>Directory.Build.props</c> read nothing on a local build, where the comment beside it said 1.0.0, and the
/// assembly carried no such metadata. A release hid that: <c>/p:Version=</c> is a global property, and a global
/// property reads the same everywhere.
/// </summary>
/// <remarks>
/// Read off MSBuild's own evaluation, as <see cref="ReceivedFileTests"/> reads its items, because the order the
/// files are imported in is the whole question and no reading of either file can answer it. Neither property
/// comes from a package, so no restore is needed. <c>Version</c> is read beside it rather than written down here:
/// the release job's <c>VERSION</c> variable reaches <c>$(Version)</c> too, MSBuild's names ignoring case.
/// </remarks>
public sealed class PublicVersionTests
{
    private const string Library = "src/DiffView.Core/DiffView.Core.csproj";

    private const string VersionProperty = "Version";

    /// <summary>With no version given, the metadata says the version the build has.</summary>
    [Fact]
    public void A_local_build_records_the_version_it_has()
    {
        (string version, string publicVersion) = Evaluate();

        Assert.True(version.Length > 0, $"{Library} evaluates to no version, so the reading found nothing to compare with.");
        Assert.True(
            publicVersion == version,
            $"{Library} evaluates to the public version \"{publicVersion}\" and the version \"{version}\", so a local build records something else.");
    }

    /// <summary>A version given the way the release gives it is the one recorded.</summary>
    [Fact]
    public void A_release_records_the_version_it_was_given()
    {
        const string Released = "2026.4.1106";

        (string version, string publicVersion) = Evaluate($"-p:Version={Released}");

        Assert.True(version == Released, $"{Library} evaluates to the version \"{version}\", so the reading never gave it {Released}.");
        Assert.True(
            publicVersion == Released,
            $"{Library} evaluates to the public version \"{publicVersion}\" given {Released}, so a release records something else.");
    }

    /// <summary>Both properties as the library evaluates to them, under whatever the arguments add.</summary>
    private static (string Version, string PublicVersion) Evaluate(params string[] arguments)
    {
        (int code, string output, string error) = ReceivedFileTests.Dotnet(
            ["msbuild", RepoPaths.Source(Library), $"-getProperty:{VersionProperty}", "-getProperty:PublicVersion", "-nodeReuse:false", .. arguments]);
        Assert.True(code == 0, $"dotnet msbuild could not evaluate {Library}:\n{output}{error}");

        using JsonDocument evaluation = JsonDocument.Parse(output);
        JsonElement properties = evaluation.RootElement.GetProperty("Properties");
        return (properties.GetProperty(VersionProperty).GetString()!, properties.GetProperty("PublicVersion").GetString()!);
    }
}
