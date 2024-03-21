using Cake.Common.IO;
using Cake.Frosting;

namespace ReleaseComponent;

[TaskName("Set Working Directory")]
public sealed class SetWorkingDirectoryTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        //Set git root folder as Working directory 
        while (!context.DirectoryExists(".git") && context.Environment.WorkingDirectory.Segments.Length > 1)
        {
            context.Environment.WorkingDirectory = context.Environment.WorkingDirectory.GetParent();
        }
    }
}