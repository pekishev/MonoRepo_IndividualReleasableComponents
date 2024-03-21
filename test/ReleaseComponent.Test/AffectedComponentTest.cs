using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cake.Common.IO;
using Cake.Core.IO;
using Cake.FileHelpers;
using Cake.Frosting;
using FluentAssertions;
using FluentAssertions.Execution;
using NuGet.Versioning;
using Xunit;

namespace ReleaseComponent.Test
{
    public class AffectedComponentTest
    {
        public static List<Exception> Exceptions;

        public static Dictionary<string, string> WrittenText;

        public static string PrefixVersion = "2.5";
        public static int PatchVersion = 3;
        public static string NewFile = @"Component1\src\Component1\NewFile.txt";


        public AffectedComponentTest()
        {
            Exceptions = new List<Exception>();
            WrittenText = new Dictionary<string, string>();
        }

        [Theory]
        [InlineData("ReadAffectedTestTask")]
        [InlineData("SortAffectedTestTask")]
        [InlineData("ReplaceProjectToPackageReferencesTestTask")]
        [InlineData("PackTestTask")]
        public void SimpleTest(string inputTask)
        {
            using (new AssertionScope())
            {
                new CakeHost()
                    .UseContext<BuildContext>()
                    .AddAssembly(typeof(ReplaceProjectToPackageReferencesTask).Assembly)
                    .AddAssembly(typeof(AffectedComponentTest).Assembly)
                    .Run(new List<string> { $"--target=\"{inputTask}\"" })
                    .Should().Be(0);

                Exceptions.Should().BeEmpty();
            }
        }


        [TaskName("PrepareTestSetupTask")]
        public class PrepareTestSetupTask : FrostingTask<BuildContext>
        {
            public override void Run(BuildContext context)
            {
                context.FileWriteText(context.Environment.WorkingDirectory.CombineWithFilePath(NewFile), "");

                context.GetLatestPackageVersion = (_, _, version) =>
                                                  {
                                                      var splitPrefix = version.Split(".");
                                                      return Task.FromResult(new NuGetVersion(int.Parse(splitPrefix[0]), int.Parse(splitPrefix[1]), PatchVersion));
                                                  };

                context.GetVersionPrefix = _ => PrefixVersion;

                context.WriteText = (file, text) => WrittenText[file.FullPath] = text;
            }

            public override void OnError(Exception exception, BuildContext context)
            {
                Exceptions.Add(exception);
            }
        }


        [TaskName("CheckReadAffectedTask")]
        public class CheckReadAffectedTask : FrostingTask<BuildContext>
        {
            public override void Run(BuildContext context)
            {
                var projects = @"Component1\src\Component1\Component1.csproj";


                var tests = @"";


                using (new AssertionScope())
                {
                    context.AffectedProjects.Should().Contain(ConvertToExpectedCollection(context, projects));
                    context.AffectedTestProjects.Should().Contain(ConvertToExpectedCollection(context, tests));
                }

                context.DeleteFile(context.Environment.WorkingDirectory.CombineWithFilePath(NewFile));
            }

            public override void OnError(Exception exception, BuildContext context)
            {
                Exceptions.Add(exception);
                context.DeleteFile(context.Environment.WorkingDirectory.CombineWithFilePath(NewFile));
            }

            private static IEnumerable<FilePath> ConvertToExpectedCollection(BuildContext context, string projects)
            {
                return projects.Split(Environment.NewLine)
                               .Select(x => context.Environment.WorkingDirectory.CombineWithFilePath(x));
            }
        }

        [TaskName("CheckSortAffectedTask")]
        public class CheckSortAffectedTask : FrostingTask<BuildContext>
        {
            public override void Run(BuildContext context)
            {
                using (new AssertionScope())
                {
                    context.ComponentVersion.Should().HaveCount(4);
                    var prefixes = PrefixVersion.Split(".").Select(int.Parse).ToArray();
                    context.ComponentVersion["Component1"].Should().Be(new NuGetVersion(prefixes[0], prefixes[1], PatchVersion + 1));
                }
            }

            public override void OnError(Exception exception, BuildContext context)
            {
                Exceptions.Add(exception);
            }
        }

        [TaskName("CheckReplaceProjectToPackageReferencesTask")]
        public class CheckReplaceProjectToPackageReferencesTask : FrostingTask<BuildContext>
        {
            public override void Run(BuildContext context)
            {
                var packagePattern = "<PackageReference Include=\"{0}\" VersionOverride=\"{1}\"";
                var internalProjectPattern = "<ProjectReference Include=\"..\\{0}\" />";
                var externalReference = "../../../";

                using (new AssertionScope())
                {
                    WrittenText.Should().HaveCount(10);

                    var component1Key = WrittenText.Keys.Should().ContainSingle(x => x.EndsWith("Component1.csproj")).Which;
                    var component1File = WrittenText[component1Key];
                    component1File.Should().Contain(string.Format(packagePattern, "Component1", $"{PrefixVersion}.{PatchVersion + 1}"));
                    component1File.Should().NotContain(externalReference);
                    
                }
            }

            public override void OnError(Exception exception, BuildContext context)
            {
                Exceptions.Add(exception);
            }
        }

        [TaskName("CheckPackTask")]
        public class CheckPackTask : FrostingTask<BuildContext>
        {
            public override void Run(BuildContext context)
            {
                using (new AssertionScope())
                {
                    var files = context.GetFiles(new GlobPattern($"{context.DestinationPath.FullPath}/*.nupkg"));

                    files.Select(x=>x.GetFilenameWithoutExtension().FullPath)
                         .Should().BeEquivalentTo(new List<string>
                                                  {
                                                      $"Component1.{PrefixVersion}.{PatchVersion + 1}",
                                                      $"Component2.{PrefixVersion}.{PatchVersion + 1}",
                                                  });

                    context.CleanDirectory(context.DestinationPath);
                }
            }

            public override void OnError(Exception exception, BuildContext context)
            {
                Exceptions.Add(exception);
                context.CleanDirectory(context.DestinationPath);
            }
        }

        [TaskName("ReadAffectedTestTask")]
        [IsDependentOn(typeof(SetWorkingDirectoryTask))]
        [IsDependentOn(typeof(PrepareTestSetupTask))]
        [IsDependentOn(typeof(RunAffectedTask))]
        [IsDependentOn(typeof(ReadAffectedTask))]
        [IsDependentOn(typeof(CheckReadAffectedTask))]
        public class ReadAffectedTestTask : FrostingTask<BuildContext> { }

        [TaskName("SortAffectedTestTask")]
        [IsDependentOn(typeof(ReadAffectedTestTask))]
        [IsDependentOn(typeof(SortAffectedTask))]
        [IsDependentOn(typeof(CheckSortAffectedTask))]
        public class SortAffectedTestTask : FrostingTask<BuildContext>  { }

        [TaskName("ReplaceProjectToPackageReferencesTestTask")]
        [IsDependentOn(typeof(SortAffectedTestTask))]
        [IsDependentOn(typeof(ReplaceProjectToPackageReferencesTask))]
        [IsDependentOn(typeof(CheckReplaceProjectToPackageReferencesTask))]
        public class ReplaceProjectToPackageReferencesTestTask : FrostingTask<BuildContext> { }

        [TaskName("PackTestTask")]
        [IsDependentOn(typeof(ReplaceProjectToPackageReferencesTestTask))]
        [IsDependentOn(typeof(PackTask))]
        [IsDependentOn(typeof(CheckPackTask))]
        public class PackTestTask : FrostingTask<BuildContext> { }

        [Fact]
        public void CheckSortOfNugetVersions()
        {
            var maxVersion = new NuGetVersion(PrefixVersion + $".{PatchVersion + 3}");
            var versions = new List<NuGetVersion>
                           {
                               new(PrefixVersion + $".{PatchVersion}"),
                               new(PrefixVersion + $".{PatchVersion+1}"),
                               new(PrefixVersion + $".{PatchVersion+1}-CI-{DateTime.Now.ToString("yyyyMMdd-HHmmss")}"),
                               new(PrefixVersion + $".{PatchVersion+2}-CI-{DateTime.Now.AddDays(1).ToString("yyyyMMdd-HHmmss")}"),
                               new(PrefixVersion + $".{PatchVersion+2}-CI-{DateTime.Now.AddDays(2).ToString("yyyyMMdd-HHmmss")}"),
                               new(PrefixVersion + $".{PatchVersion+2}-CI-{DateTime.Now.AddDays(3).ToString("yyyyMMdd-HHmmss")}"),
                               new(PrefixVersion + $".{PatchVersion+3}-CI-{DateTime.Now.AddDays(4).ToString("yyyyMMdd-HHmmss")}"),
                               maxVersion,
                               new(PrefixVersion + $".{PatchVersion+3}-CI-{DateTime.Now.AddDays(5).ToString("yyyyMMdd-HHmmss")}"),
                           };

            var maxExistingVersion = versions
                                     .Where(x => x.OriginalVersion.StartsWith(PrefixVersion))
                                     .DefaultIfEmpty()
                                     .MaxBy(x => x);

            maxExistingVersion.Should().Be(maxVersion);
        }
    }
}