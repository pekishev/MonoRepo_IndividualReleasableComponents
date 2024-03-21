using Cake.Common.Diagnostics;
using Cake.Common;
using Cake.Core.IO;
using Cake.Frosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Cake.Common.IO;

namespace ReleaseComponent
{
    [TaskName("Run Resharper Inspections")]
    public sealed class RunResharperInspectionsTask : FrostingTask<BuildContext>
    {
        public override void Run(BuildContext context)
        {
            foreach (var project in context.AffectedProjects)
            {
                Console.WriteLine($"Running inspections in project {project}");
                
                using var process = context.StartAndReturnProcess("dotnet", new ProcessSettings
                {
                    Arguments = $"jb inspectcode {project.FullPath} --build -o=ReSharper.xml -e=WARNING -f=XML -s={context.ResharperSettings}",
                });
                process.WaitForExit();
                
                var exitCode = process.GetExitCode();
                if (exitCode != 0)
                {
                    context.Warning($"Couldn't run inspections for project {project}. ExitCode: {exitCode}");
                    continue;
                }

                var filePath = context.Environment.WorkingDirectory.CombineWithFilePath("ReSharper.xml");
                foreach (var warning in ReadResharperXml(context, filePath)) 
                    context.Warning(warning);
                context.DeleteFile(filePath);
            }
        }

        private static IEnumerable<string> ReadResharperXml(BuildContext context, FilePath filePath)
        {
            var xmlFile = context.FileSystem.GetFile(filePath);
            using var xmlStream = xmlFile.OpenRead();
            using var xmlReader = XmlReader.Create(xmlStream);

            return from report in XDocument.Load(xmlReader).Elements("Report")
                from issues in report.Elements("Issues")
                from project in issues.Elements("Project")
                from issue in project.Elements("Issue")
                select $"##vso[task.logissue type=warning;sourcepath={issue.Attribute("File")?.Value};linenumber={issue.Attribute("Line")?.Value};columnnumber={issue.Attribute("Offset").Value.Split("-")[0]};code={issue.Attribute("TypeId")?.Value};]{issue.Attribute("Message")?.Value}";
        }
    }
}
