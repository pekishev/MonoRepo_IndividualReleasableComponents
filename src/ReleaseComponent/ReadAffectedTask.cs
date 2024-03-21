using System;
using System.Collections.Generic;
using System.Linq;
using Cake.Common.Diagnostics;
using Cake.Common.IO;
using Cake.Core.IO;
using Cake.FileHelpers;
using Cake.Frosting;

namespace ReleaseComponent;

[TaskName("Read Affected")]
public sealed class ReadAffectedTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        var filePath = context.Environment.WorkingDirectory.CombineWithFilePath("affected.txt");
        if (!context.FileExists(filePath))
        {
            context.Information("Affected files not found");
            Environment.Exit(0);
            return;
        }

        var affected = context.FileReadLines(filePath);
        context.Information("Read {0} items", affected.Length);
        context.DeleteFile(filePath);

        var affectedPaths = affected.Select(x => new FilePath(x)).ToList();
        var affectedProjects = new List<FilePath>();
        var affectedTestProjects = new List<FilePath>();
        affectedPaths.ForEach(x =>
        {
            if (x.Segments.Contains("Cake"))
                return;
            if (x.Segments.Contains("test"))
                affectedTestProjects.Add(x);
            else 
                affectedProjects.Add(x);
        });


        context.AffectedProjects = affectedProjects.Distinct().ToArray();
        context.Information("Filtered to {0} affected projects", context.AffectedProjects.Length);
        foreach (var affectedLine in context.AffectedProjects) 
            context.Information(affectedLine);

        context.AffectedTestProjects = affectedTestProjects.Distinct().ToArray();
        context.Information("Filtered to {0} affected test projects", context.AffectedTestProjects.Length);
        foreach (var affectedLine in context.AffectedTestProjects)
            context.Information(affectedLine);
    }

}