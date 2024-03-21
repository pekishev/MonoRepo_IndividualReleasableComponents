using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cake.Common.IO;
using Cake.Common.Security;
using Cake.Common.Solution.Project;
using Cake.Core.IO;
using Cake.Frosting;
using Cake.Incubator.Project;
using NuGet.Versioning;

namespace ReleaseComponent;

[TaskName("Sort Affected")]
public sealed class SortAffectedTask : AsyncFrostingTask<BuildContext>
{
    private List<FilePath> affectedProjectsPaths;
    private Dictionary<string, CustomProjectParserResult[]> unsortedGroupedByComponent;

    public override async Task RunAsync(BuildContext context)
    {
        affectedProjectsPaths = context.AffectedProjects.ToList();

        var workingDirLen = context.Environment.WorkingDirectory.Segments.Length;
        var groupings = affectedProjectsPaths.GroupBy(x => x.Segments[workingDirLen]);

        //Extend list with all projects from component
        foreach (var component in groupings)
        {
            var notAffectedButToBeReleased = context.GetFiles(new GlobPattern($"{component.Key}/**/*.csproj"), new GlobberSettings())
                .Where(x => x.Segments[workingDirLen + 1] != "test").Except(component).ToArray();
            affectedProjectsPaths.AddRange(notAffectedButToBeReleased);
        }

        context.AffectedProjectsByComponents = new Dictionary<string, IEnumerable<CustomProjectParserResult>>();

         unsortedGroupedByComponent = affectedProjectsPaths
                               .Select(x=> context.ParseProject(x, context.BuildConfiguration))
                               .GroupBy(x=>x.ProjectFilePath.Segments[workingDirLen])
                               .ToDictionary(x=>x.Key, x=>x.ToArray());
        foreach (var affectedProject in unsortedGroupedByComponent)
        {
            SortByDependencies(context, affectedProject.Key, affectedProject.Value);
        }

        foreach (var component in context.AffectedProjectsByComponents)
        {
            var versionPrefix = context.GetVersionPrefix(component.Key);

            var existingVersion = await GetVersionForProject(context, component.Value, versionPrefix);
            var patchVersion = existingVersion != null ? existingVersion.Patch + 1 : 0;
            var versionToRelease = new NuGetVersion($"{versionPrefix}.{patchVersion}{context.CiSuffix}");

            context.ComponentVersion[component.Key] = versionToRelease;
            Console.WriteLine($"Component {component.Key} resolved to version {versionToRelease}");
        }
    }

    private static async Task<NuGetVersion> GetVersionForProject(BuildContext context, IEnumerable<CustomProjectParserResult> components, string versionPrefix)
    {
        var res = new List<NuGetVersion>();

        foreach (var projectParserResult in components)
        {
            var packageId = projectParserResult.NetCore.PackageId;
            var existingVersion = await context.GetLatestPackageVersion(context, packageId, versionPrefix);
            if (existingVersion != null)
                //return existingVersion;
                res.Add(existingVersion);
        }

        return res.DefaultIfEmpty().Max();

        //return null;
    }


    private void SortByDependencies(BuildContext context, string key, CustomProjectParserResult[] items)
    {
        if (context.AffectedProjectsByComponents.ContainsKey(key))
            return;

        var referenceComponents = items.SelectMany(x => x.ProjectReferences
                                                         .Select(y => context
                                                                      .ParseProject(y.FilePath, context.BuildConfiguration)
                                                                      .ProjectFilePath.Segments[context.Environment.WorkingDirectory.Segments.Length]))
                                       .Distinct()
                                       .Except(new[] { key })
                                       .ToArray();
        foreach (var referenceComponent in referenceComponents)
        {
            if (unsortedGroupedByComponent.TryGetValue(referenceComponent, out var subGrouping))
                SortByDependencies(context, referenceComponent, subGrouping);
        }

        context.AffectedProjectsByComponents[key] = items;
    }
}