using Cake.Common.Diagnostics;
using Cake.Common;
using Cake.Core.IO;
using Cake.Frosting;
using System;
using System.Text;
using System.Text.RegularExpressions;

namespace ReleaseComponent
{
    [TaskName("Run UnitTest")]
    public sealed class RunUnitTestTask : FrostingTask<BuildContext>
    {
        public override void Run(BuildContext context)
        {
            var failed = false;

            foreach (var project in context.AffectedTestProjects)
            {
                Console.WriteLine($"Running tests in project {project}");

                var arguments = new StringBuilder();
                arguments.Append($"test {project.FullPath}");
                arguments.Append($" --configuration {context.BuildConfiguration}");
//                arguments.Append(" --DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.CodeCoverage.UseVerifiableInstrumentation=False");
                arguments.Append("  --verbosity minimal");
                arguments.Append(" --collect \"Code Coverage\"");
                arguments.Append(" -l:trx");
                arguments.Append(" -p:CIRun=true");

                using var process = context.StartAndReturnProcess("dotnet", new ProcessSettings
                {
                    Arguments = arguments.ToString(),
                    RedirectStandardOutput = true,
                    RedirectedStandardOutputHandler = ProcessTestOutput,
                });
                process.WaitForExit();
                
                var exitCode = process.GetExitCode();
                if (exitCode != 0) 
                    failed = true;

                context.Information($"Running tests in {project}. ExitCode: {exitCode}");
            }

            context.Information($"Test run finished. Passed: {numTestsPassed}, Failed: {numTestsFailed}");

            if (numTestsFailed > 0)
            {
                context.Error($"##vso[task.logissue type=error;]{numTestsFailed} test(s) failed. Look at the TESTS tab for details.");
                Environment.Exit(1);
            }
            if (failed)
            {
                context.Error("Some unit test run didn't return successful exit code.");
                Environment.Exit(1);
            }
        }

        private int numTestsFailed = 0;
        private int numTestsPassed = 0;

        private static Regex testResultRegex = new(@"(\w+)!\s+-\s+Failed:\s+(\d+),\s+Passed:\s+(\d+),\s+Skipped:\s+(\d+),\s+Total:\s+(\d+)",RegexOptions.Compiled );

        private string ProcessTestOutput(string arg)
        {
            if (!string.IsNullOrWhiteSpace(arg) && testResultRegex.IsMatch(arg))
            {
                var match = testResultRegex.Match(arg);
                numTestsFailed += int.Parse(match.Groups[2].Value);
                numTestsPassed += int.Parse(match.Groups[3].Value);
            }

            Console.WriteLine(arg);
            return null;
        }
    }
}
