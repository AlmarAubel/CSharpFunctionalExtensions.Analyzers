using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Nuke.Common;
using Nuke.Common.ChangeLog;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitHub;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.Utilities.Collections;
using Octokit;
using Octokit.Internal;
using Serilog;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

[GitHubActions(
    "continuous",
    GitHubActionsImage.UbuntuLatest,
    On = new[] { GitHubActionsTrigger.Push },
    EnableGitHubToken = true,
    AutoGenerate = false,
    InvokedTargets = new[] { nameof(Pack) }
)]
[GitHubActions(
    "publish",
    GitHubActionsImage.UbuntuLatest,
    On = new[] { GitHubActionsTrigger.WorkflowDispatch },
    InvokedTargets = new[] { nameof(Push) },
    EnableGitHubToken = true,
    AutoGenerate = false,
    ImportSecrets = new[] { nameof(NugetApiKey) }
)]
class Build : NukeBuild
{
    [Nuke.Common.Parameter] [Secret] readonly string NugetApiKey;
    [Nuke.Common.Parameter] readonly string NugetApiUrl = "https://api.nuget.org/v3/index.json"; //Path.GetTempPath();
    
    [GitRepository] readonly GitRepository Repository;

    [GitVersion] readonly GitVersion GitVersion;
    [Nuke.Common.Parameter("Artifacts Type")]readonly string ArtifactsType;

    static GitHubActions GitHubActions => GitHubActions.Instance;
    static AbsolutePath ArtifactsDirectory => RootDirectory / ".artifacts";
    static AbsolutePath SourceDirectory => RootDirectory;
    static AbsolutePath OutputDirectory => RootDirectory / "output";
    
    static readonly string PackageContentType = "application/octet-stream";
    static string ChangeLogFile => RootDirectory / "CHANGELOG.md";
    string GithubNugetFeed => GitHubActions != null
        ? $"https://nuget.pkg.github.com/{GitHubActions.RepositoryOwner}/index.json"
        : null;
    public static int Main() => Execute<Build>(x => x.Compile);

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
        .Triggers(PublishToGithub)
        .Executes(() =>
        {
            Log.Information("GitVersion = {Value}", GitVersion.MajorMinorPatch);
            DotNetPack(s =>
                s.SetConfiguration("Release")
                    .EnableNoBuild()
                    .SetVersion(GitVersion.NuGetVersionV2)
                    .SetOutputDirectory(ArtifactsDirectory));
        });
    
    Target PublishToGithub => _ => _
        .Description($"Publishing to Github for Development only.")
        .Triggers(CreateRelease)
        .OnlyWhenStatic(() => Repository.IsOnDevelopBranch() || GitHubActions.IsPullRequest)
        .Executes(() =>
        {
            ArtifactsDirectory.GlobFiles(ArtifactsType)
                .ForEach(x =>
                {
                    DotNetNuGetPush(s => s
                        .SetTargetPath(x)
                        .SetSource(GithubNugetFeed)
                        .SetApiKey(GitHubActions.Token)
                        .EnableSkipDuplicate()
                    );
                });
        });
    Target Push => _ => _
        .DependsOn(Pack)
        .Requires(() => NugetApiUrl)
        .Requires(() => NugetApiKey)
        .Executes(() =>
        {
            DotNetNuGetPush(s =>
                s.SetSource(NugetApiUrl)
                    .SetApiKey(NugetApiKey)
                    .CombineWith(
                        ArtifactsDirectory.GlobFiles("*.nupkg").NotEmpty(),
                        (_, v) => _.SetTargetPath(v)
                    )
            );
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

    Target CreateRelease => _ => _
        .Description($"Creating release for the publishable version.")
        .OnlyWhenStatic(() =>Repository.IsOnMainOrMasterBranch()|| Repository.IsOnReleaseBranch())
        .Executes(async () =>
        {
            var credentials = new Credentials(GitHubActions.Token);
            GitHubTasks.GitHubClient = new GitHubClient(new ProductHeaderValue(nameof(NukeBuild)),
                new InMemoryCredentialStore(credentials));

            var (owner, name) = (Repository.GetGitHubOwner(), Repository.GetGitHubName());

            var releaseTag = GitVersion.NuGetVersionV2;
            var changeLogSectionEntries = ChangelogTasks.ExtractChangelogSectionNotes(ChangeLogFile);
            var latestChangeLog = changeLogSectionEntries
                .Aggregate((c, n) => c + Environment.NewLine + n);

            var newRelease = new NewRelease(releaseTag)
            {
                TargetCommitish = GitVersion.Sha,
                Draft = true,
                Name = $"v{releaseTag}",
                Prerelease = !string.IsNullOrEmpty(GitVersion.PreReleaseTag),
                Body = latestChangeLog
            };

            var createdRelease = await GitHubTasks
                .GitHubClient
                .Repository
                .Release.Create(owner, name, newRelease);

            OutputDirectory.GlobFiles( ArtifactsType)
                .ForEach(async x => await UploadReleaseAssetToGithub(createdRelease, x));

            await GitHubTasks
                .GitHubClient
                .Repository
                .Release
                .Edit(owner, name, createdRelease.Id, new ReleaseUpdate { Draft = false });
        });


    private static async Task UploadReleaseAssetToGithub(Release release, string asset)
    {
        await using var artifactStream = File.OpenRead(asset);
        var fileName = Path.GetFileName(asset);
        var assetUpload = new ReleaseAssetUpload { FileName = fileName, ContentType = PackageContentType, RawData = artifactStream, };
        await GitHubTasks.GitHubClient.Repository.Release.UploadAsset(release, assetUpload);
    }
}
