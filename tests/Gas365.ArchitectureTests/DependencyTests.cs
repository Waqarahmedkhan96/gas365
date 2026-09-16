using System.Xml.Linq;

namespace Gas365.ArchitectureTests;

public sealed class DependencyTests
{
    [Theory]
    [InlineData("Gas365.Domain", new string[] { })]
    [InlineData("Gas365.Application", new[] { "Gas365.Domain" })]
    [InlineData("Gas365.Infrastructure", new[] { "Gas365.Application" })]
    [InlineData("Gas365.Api", new[] { "Gas365.Application", "Gas365.Infrastructure" })]
    public void ProjectReferencesRespectLayerBoundaries(string project, string[] allowed)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Gas365.sln")))
        {
            root = root.Parent;
        }
        Assert.NotNull(root);
        var document = XDocument.Load(Path.Combine(root.FullName, "src", project, project + ".csproj"));
        var references = document.Descendants("ProjectReference")
            .Select(element => Path.GetFileNameWithoutExtension(element.Attribute("Include")!.Value));
        Assert.All(references, reference => Assert.Contains(reference, allowed));
        if (project == "Gas365.Domain")
        {
            Assert.Empty(document.Descendants("PackageReference"));
            Assert.Empty(document.Descendants("FrameworkReference"));
        }
    }
}
