using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cake.Core;
using Cake.Core.IO;
using Cake.FileHelpers;
using Cake.Frosting;
using Cake.Incubator;
using Cake.Incubator.Project;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace ReleaseComponent;

public static class Program
{
    public static int Main(string[] args)
    {
        Lib2GitNativePathHelper.ResolveCustomNativeLibraryPath();

        return new CakeHost()
            .UseContext<BuildContext>()
            .Run(args);
    }
}

public class BuildContext : FrostingContext
{
    public string BuildConfiguration { get; init; }
    public string MainBranchName { get; init; }
    public string NugetFeed { get; init; }
    public string NugetUserName { get; init; }
    public string NugetPassword { get; init; }

    public string FromCommit { get; set; }
    public string ToCommit { get; set; }
    public string ResharperSettings { get; set; }

    public FindPackageByIdResource NugetConnection { get; set; }
    public FilePath[] AffectedProjects { get; set; }
    public FilePath[] AffectedTestProjects { get; set; }
    public Dictionary<string, IEnumerable<CustomProjectParserResult>> AffectedProjectsByComponents { get; set; }
    public Dictionary<string, NuGetVersion> ComponentVersion { get; set; } = new();

    public Func<BuildContext, string, string, Task<NuGetVersion>> GetLatestPackageVersion { get; set; }
    public Func<string, string> GetVersionPrefix { get; set; }
    public Action<FilePath, string> WriteText { get; set; }

    public DirectoryPath DestinationPath { get; set; }
    public string CiSuffix { get; set; }

    public BuildContext(ICakeContext context)
        : base(context)
    {
        BuildConfiguration = context.Arguments.GetArgument("configuration") ?? "Release";
        MainBranchName = context.Arguments.GetArgument("MainBranch") ?? "origin/main";//this is needed to support lts branches to specify target main branch
        NugetFeed = context.Arguments.GetArgument("NugetFeed");
        NugetUserName = context.Arguments.GetArgument("NugetUser");
        NugetPassword = context.Arguments.GetArgument("NugetPassword");
        ResharperSettings = context.Arguments.GetArgument("ResharperSettings") ?? "ReleaseComponent.sln.DotSettings";

        GetLatestPackageVersion = PackageUtils.GetLatestNugetVersion;
        GetVersionPrefix = this.GetExistingVersionPrefix;
        WriteText = this.FileWriteText;
    }
}

[TaskName("Default")]
[IsDependentOn(typeof(SetWorkingDirectoryTask))]
[IsDependentOn(typeof(FindLastCommitTask))]
[IsDependentOn(typeof(RunAffectedTask))]
[IsDependentOn(typeof(ReadAffectedTask))]
[IsDependentOn(typeof(ConnectToNugetTask))]
[IsDependentOn(typeof(SortAffectedTask))]
[IsDependentOn(typeof(ReplaceProjectToPackageReferencesTask))]
[IsDependentOn(typeof(PackTask))]
[IsDependentOn(typeof(SetTagsTask))]
// ReSharper disable once UnusedType.Global
public class DefaultTask : FrostingTask
{ }

[TaskName("unitTest")]
[IsDependentOn(typeof(SetWorkingDirectoryTask))]
[IsDependentOn(typeof(FindLastCommitTask))]
[IsDependentOn(typeof(RunAffectedTask))]
[IsDependentOn(typeof(ReadAffectedTask))]
[IsDependentOn(typeof(RunUnitTestTask))]
public class UnitTestTask : FrostingTask
{ }

[TaskName("inspections")]
[IsDependentOn(typeof(SetWorkingDirectoryTask))]
[IsDependentOn(typeof(FindLastCommitTask))]
[IsDependentOn(typeof(RunAffectedTask))]
[IsDependentOn(typeof(ReadAffectedTask))]
[IsDependentOn(typeof(RunResharperInspectionsTask))]
public class InspectionsTask : FrostingTask
{ }