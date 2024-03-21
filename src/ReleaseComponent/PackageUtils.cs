using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cake.Common.IO;
using Cake.Core.IO;
using Cake.FileHelpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace ReleaseComponent;

public static class PackageUtils
{
    public static string GetExistingVersionPrefix(this BuildContext context, string component)
    {
        var fullDir = context.Environment.WorkingDirectory.Combine(component);
        var fileName = fullDir.CombineWithFilePath(new FilePath("PackageVersions.json"));

        if (!context.FileExists(fileName))
            throw new FileNotFoundException($"For component {component} file PackageVersions.json was not found.");

        var packageVersion = context.FileReadText(fileName);

        var obj = (JObject)JsonConvert.DeserializeObject(packageVersion);
        var version = obj?.SelectToken("versionPrefix")?.Value<string>();
        return version ?? throw new ArgumentException($"For component {component} file PackageVersions.json either corrupted or was not included \"versionPrefix\" property.");
    }

    public static async Task<NuGetVersion> GetLatestNugetVersion(BuildContext context, string packageId, [NotNull] string versionPrefix)
    {
        var logger = NullLogger.Instance;
        var cancellationToken = CancellationToken.None;

        var cache = new SourceCacheContext();
        var versions = await context.NugetConnection.GetAllVersionsAsync(packageId,
                                                                  cache,
                                                                  logger,
                                                                  cancellationToken);

        var maxExistingVersion = versions
                                 .Where(x => x.OriginalVersion.StartsWith(versionPrefix))
                                 .Where(x => !x.IsPrerelease)
                                 .DefaultIfEmpty()
                                 .MaxBy(x => x);

        return maxExistingVersion;
    }
}