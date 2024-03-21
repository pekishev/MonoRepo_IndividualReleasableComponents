using System;
using System.Linq;
using Cake.Core.Diagnostics;
using Cake.Frosting;
using LibGit2Sharp;

namespace ReleaseComponent;

[TaskName("Find last release commit")]
[IsDependentOn(typeof(SetWorkingDirectoryTask))]
public sealed class FindLastCommitTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        var repoPath = context.Environment.WorkingDirectory;
        try
        {
            using var repo = new Repository(repoPath.FullPath);

            var mainHead = repo.Branches[context.MainBranchName].Tip;
            var isMain = repo.Head.Tip == mainHead;
            
            context.CiSuffix = isMain ? string.Empty : "-CI-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");

            Commit commitToCompare;
            var currentCommit = repo.Head.Tip;
            context.Log.Information("Current commit:" + currentCommit.Message);

            if (isMain || currentCommit.Message.StartsWith("Merge pull request")) //This is virtual commit from Azure DevOps

                commitToCompare = currentCommit.Parents.ElementAt(0);
            else
            {
                context.Log.Information("Repo branches:" + string.Join(",", repo.Branches.Select(x=>x.FriendlyName)));

                commitToCompare = mainHead;

                if (!repo.Head.Commits.Contains(mainHead))
                {
                    context.Log.Error($"Current branch does not contain latest \'{context.MainBranchName.Split("/").Last()}\' branch head.");
                    Environment.Exit(1);
                }
            }

            context.FromCommit = commitToCompare.Sha;
            context.ToCommit = currentCommit.Sha;

        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
        }
    }
}
