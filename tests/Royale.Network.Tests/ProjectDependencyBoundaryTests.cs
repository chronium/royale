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

    [Fact]
    public void ClientDoesNotReferenceServerProject()
    {
        string root = FindRepositoryRoot();
        string clientProject = Path.Combine(root, "src", "Royale.Client", "Royale.Client.csproj");

        string[] references = ReadProjectReferences(clientProject);

        Assert.DoesNotContain(references, reference => reference.Contains("Royale.Server", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ServerDoesNotReferenceClientRenderingOrUiProjects()
    {
        string root = FindRepositoryRoot();
        string serverProject = Path.Combine(root, "src", "Royale.Server", "Royale.Server.csproj");

        string[] references = ReadProjectReferences(serverProject);

        Assert.DoesNotContain(references, reference => reference.Contains("Royale.Client", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(references, reference => reference.Contains("SDL3-CS", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(references, reference => reference.Contains("ImGui", StringComparison.OrdinalIgnoreCase));
    }

    private static string[] ReadProjectReferences(string projectPath)
    {
        XDocument project = XDocument.Load(projectPath);
        return project.Descendants("ProjectReference")
            .Select(reference => reference.Attribute("Include")?.Value)
            .Where(value => value is not null)
            .Cast<string>()
            .ToArray();
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
