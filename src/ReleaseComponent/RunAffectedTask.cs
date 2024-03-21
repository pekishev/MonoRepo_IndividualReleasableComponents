using Cake.Common;
using Cake.Common.Diagnostics;
using Cake.Core.IO;
using Cake.Frosting;

namespace ReleaseComponent;

[TaskName("Run Affected")]
public sealed class RunAffectedTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        string fromCommand = string.Empty;
        if (context.FromCommit != null)
        {
            fromCommand = $"--from {context.FromCommit} --to {context.ToCommit}";
            context.Information($"Comparing diff between commits {context.FromCommit} and {context.ToCommit}");
        }

        using var process = context.StartAndReturnProcess("dotnet", 
                                                          new ProcessSettings
                                                          {
                                                              Arguments = $"tool run dotnet-affected --format text {fromCommand}",
                                                          });
        process.WaitForExit();
        context.Information("Affected Exit code: {0}", process.GetExitCode());
    }
}