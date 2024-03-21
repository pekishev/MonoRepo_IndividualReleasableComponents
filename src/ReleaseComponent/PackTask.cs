using System;
using System.Linq;
using Cake.Common;
using Cake.Common.Diagnostics;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Frosting;

namespace ReleaseComponent;

[TaskName("Call Dotnet pack")]
public class PackTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        var tempSubFolder = context.Arguments.GetArgument("OutTempDir") ?? new DirectoryPath(context.Environment.GetEnvironmentVariable("TEMP"));
        context.DestinationPath = tempSubFolder.Combine("_FF_NUGET_TEST");

        if (!context.DirectoryExists(context.DestinationPath))
            context.CreateDirectory(context.DestinationPath);

        context.CleanDirectory(context.DestinationPath);

        var failed = false;
        foreach (var component in context.AffectedProjectsByComponents)
        {
            context.Log.Information($"Releasing component: {component.Key}");
            if (!context.ComponentVersion.TryGetValue(component.Key, out var versionToRelease))
                continue;

            var projectsToRelease = component.Value.Select(x => x.ProjectFilePath).ToArray();

            foreach (var project in projectsToRelease)
            {
                Console.WriteLine($"component {component.Key}  have to be released {versionToRelease}");


                using var process = context.StartAndReturnProcess("dotnet",
                                                                  new ProcessSettings
                                                                  {
                                                                      Arguments = $"pack {project.FullPath} --output {context.DestinationPath} --configuration {context.BuildConfiguration} -p:Version={versionToRelease}",
                                                                  });
                process.WaitForExit();
                var exitCode = process.GetExitCode();
                if (exitCode != 0)
                    failed = true;

                context.Information("dotnet pack exit code: {0}", exitCode);
            }

        }
        if (failed)
        {
            context.Error($"##vso[task.logissue type=error;]Couldn't pack some components.");
            Environment.Exit(1);
        }
    }
}