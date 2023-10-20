using Nuke.Common;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Serilog;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

[GitHubActions(
    "continuous",
    GitHubActionsImage.UbuntuLatest,
    On = new[] { GitHubActionsTrigger.Push },
    InvokedTargets = new[] { nameof(Test) }
)]
[GitHubActions(
    "publish",
    GitHubActionsImage.UbuntuLatest,
    On = new[] { GitHubActionsTrigger.WorkflowDispatch },
    InvokedTargets = new[] { nameof(Push) }
)]
 class Build : NukeBuild
{
    [Parameter] readonly string NugetApiKey;
    AbsolutePath SourceDirectory => RootDirectory;
    AbsolutePath OutputDirectory => RootDirectory / "output";
    
    [GitRepository] readonly GitRepository Repository;
    
    public static int Main()=> Execute<Build>(x=> x.Compile);

    Target Clean => _ => _
        .Executes(() =>
        {
            FileSystemTasks.DeleteDirectory(OutputDirectory);
            FileSystemTasks.EnsureCleanDirectory(OutputDirectory);
        });

    Target Restore => _ => _
        .DependsOn(Clean)
        .Executes(() =>
        {
            DotNetRestore(s => s.SetProjectFile(SourceDirectory));
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetBuild(s => s.SetProjectFile(SourceDirectory).SetConfiguration("Release"));
        });

    Target Test => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            DotNetTest(s => s.SetProjectFile(SourceDirectory).SetConfiguration("Release"));
        });

    Target Publish => _ => _
        .DependsOn(Test)
        .Executes(() =>
        {
            DotNetPublish(s => s.SetConfiguration("Release").SetOutput(OutputDirectory));
        });

    Target Pack => _ => _
        .DependsOn(Publish)
        .Executes(() =>
        {
            DotNetPack(s => s.SetConfiguration("Release").EnableNoBuild().SetOutputDirectory(OutputDirectory));
        });

    Target Push => _ => _
        .DependsOn(Pack)
        .Executes(() =>
        {
            // DotNetNuGetPush(
            //     s =>
            //         s.SetSource("https://api.nuget.org/v3/index.json")
            //             .SetApiKey(NugetApiKey)
            //             .CombineWith(
            //                 FileSystemTasks.GlobFiles(OutputDirectory, "*.nupkg").NotEmpty(),
            //                 (_, v) => _.SetTargetPath(v)
            //             )
            // );
        });

    Target Print => _ => _
        .Executes(() =>
        {
            Log.Information("Commit = {Value}", Repository.Commit);
            Log.Information("Branch = {Value}", Repository.Branch);
            Log.Information("Tags = {Value}", Repository.Tags);
    
            Log.Information("main branch = {Value}", Repository.IsOnMainBranch());
            Log.Information("main/master branch = {Value}", Repository.IsOnMainOrMasterBranch());
            Log.Information("release/* branch = {Value}", Repository.IsOnReleaseBranch());
            Log.Information("hotfix/* branch = {Value}", Repository.IsOnHotfixBranch());
    
            Log.Information("Https URL = {Value}", Repository.HttpsUrl);
            Log.Information("SSH URL = {Value}", Repository.SshUrl);
        });
}
