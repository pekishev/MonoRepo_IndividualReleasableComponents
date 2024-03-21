using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cake.Core.Diagnostics;
using Cake.FileHelpers;
using Cake.Frosting;
using Cake.Incubator.Project;
using NuGet.Versioning;

namespace ReleaseComponent;

[TaskName("ReplaceProjectToPackageReferences")]
public sealed class ReplaceProjectToPackageReferencesTask : AsyncFrostingTask<BuildContext>
{
    public override async Task RunAsync(BuildContext context)
    {
        foreach (var component in context.AffectedProjectsByComponents)
        {
            context.Log.Information($"Replacing references in component: {component.Key}");
            foreach (var project in component.Value)
            {
                await ReplaceProjectWithPackage(context, project);
            }
        }
    }

    private async Task ReplaceProjectWithPackage(BuildContext context, CustomProjectParserResult project)
    {
        var componentVersion = new Dictionary<string,NuGetVersion>(context.ComponentVersion) ;

        var contents = context.FileReadText(project.ProjectFilePath);

        var referencesReplaced = 0;
        foreach (var projectReference in project.ProjectReferences.Where(x=>x.RelativePath.StartsWith(@"../../../")))
        {
            var projectPattern = $"<ProjectReference Include=\"{projectReference.RelativePath}\"";
            var workingDirLen = context.Environment.WorkingDirectory.Segments.Length;
            var refProjectId = projectReference.Name;
            var refComponent = projectReference.FilePath.Segments[workingDirLen];

            if (!componentVersion.TryGetValue(refComponent, out var refPackageVersion))
            {
                var versionPrefix = context.GetVersionPrefix(refComponent);

                refPackageVersion = await context.GetLatestPackageVersion(context, refProjectId, versionPrefix);
                componentVersion.Add(refComponent, refPackageVersion);
            }
            var packagePattern = $"<PackageReference Include=\"{refProjectId}\" VersionOverride=\"{refPackageVersion}\"";

            contents = contents.Replace(projectPattern.Replace("/", "\\"), packagePattern);
            referencesReplaced++;
        }

        if (referencesReplaced > 0)
        {
            context.Log.Information($"Replacing references in project: {project.ProjectFilePath}");
            context.WriteText(project.ProjectFilePath, contents);
        }
    }
}