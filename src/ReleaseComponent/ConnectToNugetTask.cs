using System;
using System.Threading;
using System.Threading.Tasks;
using Cake.Core.Diagnostics;
using Cake.Frosting;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace ReleaseComponent;

[TaskName("ConnectToNuget")]
public sealed class ConnectToNugetTask : AsyncFrostingTask<BuildContext>
{
    // Tasks can be asynchronous
    public override async Task RunAsync(BuildContext context)
    {
        var packageSource = new PackageSource(context.NugetFeed)
                            {
                                Credentials = new PackageSourceCredential(
                                                                          source: context.NugetFeed,
                                                                          username:  context.NugetUserName,
                                                                          passwordText: context.NugetPassword,
                                                                          isPasswordClearText: true,
                                                                          validAuthenticationTypesText: null)
                            };
        var repository = Repository.Factory.GetCoreV3(packageSource);
        try
        {
            context.NugetConnection = await repository.GetResourceAsync<FindPackageByIdResource>(CancellationToken.None);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }

        context.Log.Information("Connected to nuget");
    }
}