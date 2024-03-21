using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cake.Common.IO;
using Cake.Core.Diagnostics;
using Cake.Frosting;
using LibGit2Sharp;

namespace ReleaseComponent;

public class SetTagsTask:FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        var sb = new StringBuilder();
        var repoPath = context.Environment.WorkingDirectory;
        try
        {
            using var repo = new Repository(repoPath.FullPath);
            var commit = repo.Head.Tip;

            var branchName = repo.Branches.Where(x=>x.Tip == commit).Select(x=>x.FriendlyName.Split("/").Last()).FirstOrDefault() ?? "NoName";

            var tagName = $"Release-{branchName}{context.CiSuffix}";
            if (string.IsNullOrEmpty(context.CiSuffix)) 
                tagName = tagName + "-" + DateTime.Now.ToString("yyyyMMdd");

            context.Log.Information("Adding release tag:" + tagName);

            foreach (var component in context.AffectedProjectsByComponents)
            {
                if (!context.ComponentVersion.TryGetValue(component.Key, out var versionToRelease))
                    continue;

                var tag = $" {component.Key}_{versionToRelease}";
                sb.Append(tag);
                context.Log.Information(tag);
            }

            var tagger = new Signature("CI", "ci@ci.com", DateTimeOffset.Now);
            var tagObj = repo.Tags.Add(tagName, commit, tagger, sb.ToString());

            //deletes tags file if exists
            var tagsFile = "tags.txt";
            var withFilePath = context.Environment.WorkingDirectory.CombineWithFilePath(tagsFile);
            if (context.FileExists(withFilePath))
                context.DeleteFile(withFilePath);
            context.WriteText(withFilePath, tagObj.CanonicalName);
        }
        catch (Exception e)
        {
            context.Log.Information(e.ToString());
        }
    }
}