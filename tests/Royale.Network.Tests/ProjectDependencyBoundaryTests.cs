using System.Xml.Linq;

namespace Royale.Network.Tests;

public sealed class ProjectDependencyBoundaryTests
{
    [Fact]
    public void ProtocolDoesNotReferenceNetwork()
    {
        string root = FindRepositoryRoot();
        string protocolProject = Path.Combine(root, "src", "Royale.Protocol", "Royale.Protocol.csproj");
        XDocument project = XDocument.Load(protocolProject);

        IEnumerable<string> references = project.Descendants("ProjectReference")
            .Select(reference => reference.Attribute("Include")?.Value)
            .Where(value => value is not null)
            .Cast<string>();

        Assert.DoesNotContain(references, reference => reference.Contains("Royale.Network", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NetworkIsOnlyProductionProjectReferencingLiteNetLib()
    {
        string root = FindRepositoryRoot();
        string src = Path.Combine(root, "src");

        string[] projectsReferencingLiteNetLib = Directory.GetFiles(src, "*.csproj", SearchOption.AllDirectories)
            .Where(projectPath => File.ReadAllText(projectPath).Contains("LiteNetLib.csproj", StringComparison.OrdinalIgnoreCase))
            .Select(projectPath => Path.GetRelativePath(root, projectPath).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["src/Royale.Network/Royale.Network.csproj"], projectsReferencingLiteNetLib);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Royale.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not find repository root.");
    }
}
