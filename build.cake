#module nuget:?package=Cake.DotNetTool.Module&version=0.3.0

//////////////////////////////////////////////////////////////////////
// TOOLS
//////////////////////////////////////////////////////////////////////
#tool "dotnet:https://api.nuget.org/v3/index.json?package=Octopus.DotNet.Cli&version=7.4.2"
#tool "dotnet:https://api.nuget.org/v3/index.json?package=GitVersion.Tool&version=5.3.8"

////////////////////////////////////////////////////////////////////// 
// ADDINS
//////////////////////////////////////////////////////////////////////
#addin "nuget:https://api.nuget.org/v3/index.json?package=Cake.Docker&version=0.10.1"
#addin "nuget:https://api.nuget.org/v3/index.json?package=Cake.FileHelpers&version=3.2.1"

//////////////////////////////////////////////////////////////////////
// NAMESPACES
//////////////////////////////////////////////////////////////////////
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using SystemTask = System.Threading.Tasks.Task;
using System.Net;
using System.Net.Http;

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////
var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");
var ecsRepository = EnvironmentVariable("ECS_REPOSITORY");
var region = EnvironmentVariable("AWS_REGION") ?? "eu-west-1";
var ecrAccountId = EnvironmentVariable("ECR_ACCOUNT_ID") ?? "791259062566";

var buildArtifacts = "./artifacts";
var isCIBuild = !BuildSystem.IsLocalBuild;

var dockerUsed = false;
var dockerComposeFiles = new[] { "docker-compose.yml", "docker-compose.apps.yml", "docker-compose.logging.yml" };

HttpClient client = new HttpClient();
const int apiHealthCheckAttempts = 25;
const int apiHealthCheckIntervalMs = 3000;

GitVersion gitVersionInfo;
Project[] solution;
string nugetVersion;

Setup(context =>
{
    gitVersionInfo = GitVersion (new GitVersionSettings {
        OutputType = GitVersionOutput.Json
    });

    nugetVersion = gitVersionInfo.NuGetVersion;

    ecsRepository = string.IsNullOrWhiteSpace(ecsRepository)
                ? "local"
                : ecsRepository;

    solution = new []
    {
        CreateProject("Ditto",   "./src/Ditto/Dockerfile", gitVersionInfo, "ditto"),
    };

    Information("Building Ditto v{0} with configuration {1}", nugetVersion, configuration);
});

Task("__Clean")
    .Does(() => 
    {
        CleanDirectories(buildArtifacts);
    });
Task("__Build")
    .Does(() =>
    {
        var buildSettings = new DotNetCoreBuildSettings
        {
            Configuration = configuration
        };

        var solutionFile = GetFiles("*.sln").FirstOrDefault();

        if (solutionFile != null)
        {
            DotNetCoreBuild(solutionFile.GetFilename().ToString(), buildSettings);
        }
        else
        {
            throw new Exception("Solution file not found.");
        }
    });

Task("__Test")
    .Does(() =>
    {
        // who needs tests
    });

Task("__DockerLogin")
    .Does(() =>
    {
        var getLoginSettings = new ProcessSettings
        {
            Arguments = $"ecr get-login-password --region {region}",
            RedirectStandardOutput = true
        };

        string dockerLoginCmd;
        using(var process = StartAndReturnProcess("aws", getLoginSettings))
        {
            dockerLoginCmd = process.GetStandardOutput().ElementAt(0);
            process.WaitForExit();
        }

        var ecrUrl = $"{ecrAccountId}.dkr.ecr.{region}.amazonaws.com";
        DockerLogin("AWS", dockerLoginCmd, ecrUrl);
    });

Task("__DockerBuild")
    .Does(async () =>
    {
        await RunForEachProject(project =>
        {
            var tags = CreateTags(project); 
            var settings = new DockerImageBuildSettings
            {
                File = project.DockerImagePath,
                BuildArg = new[] {
                    $"BUILDCONFIG={configuration}",
                    $"VERSION={nugetVersion}"
                },
                Tag = tags
            };

            DockerBuild(settings, ".");
        });
    });

Task("__DockerPush")
    .Does(async () =>
    {
        await RunForEachProject(project =>
        {
            foreach (var tag in project.Tags)
            {
                 DockerPush(tag);
            }
        });
    });
Task("Build")
    .IsDependentOn("__Clean")
    .IsDependentOn("__Build")
    .IsDependentOn("__Test");

Task("Deploy")
    .IsDependentOn("__DockerBuild")
    .IsDependentOn("__DockerLogin")
    .IsDependentOn("__DockerPush");

Task("Default")
    .IsDependentOn("Build");

RunTarget(target);

///////////////////////////////////////////////////////////////////////////////
// Helpers
///////////////////////////////////////////////////////////////////////////////
public class Project
{
    public string OctopusProjectName { get; set; }
    public string DockerImagePath { get; set; }
    public string Version { get; set; }
    public string ContainerName { get; set; }
    public bool IsPreRelease { get; set; }
    public string[] Tags { get; set; }
}

SystemTask RunForEachProject(Action<Project> action)
{
    var aggregate = solution.Select(project => SystemTask.Run(() => action.Invoke(project)));
    return SystemTask.WhenAll(aggregate);
}

Project CreateProject(string octopusProjectName, string dockerImagePath, GitVersion gitVersionInfo, string containerName)
{
    return new Project
    {
        OctopusProjectName = octopusProjectName,
        DockerImagePath = dockerImagePath,
        Version = gitVersionInfo.NuGetVersion,
        IsPreRelease = string.IsNullOrWhiteSpace(gitVersionInfo.PreReleaseLabel),
        ContainerName = containerName
    };
}


string[] CreateTags(Project project)
{
    var mgmtRepository = ecrAccountId + ".dkr.ecr.eu-west-1.amazonaws.com";
    string prefix = $"{mgmtRepository}/cko-gateway/{project.ContainerName}";

    var tags = project.IsPreRelease
            ? new[] { $"{prefix}:{nugetVersion}", $"{prefix}:prerelease" }
            : new[] { $"{prefix}:{nugetVersion}", $"{prefix}:latest" };
    project.Tags = tags;
    return tags;
} 

async Task EnsureApiIsHealthy(string apiUrl, string applicationName)
{
    var attempts = 0;
    while (attempts < apiHealthCheckAttempts)
    {
        attempts++;
        var healthCheckResult = await CheckHttpHealth(apiUrl + "/_system/health");
        if (healthCheckResult.Success)
        {
            break;
        }
        else
        {
            Information($"{applicationName} unhealthy ({healthCheckResult.Status}). Retrying in {apiHealthCheckIntervalMs/1000} seconds (attempt {attempts}/{apiHealthCheckAttempts}).");
            Thread.Sleep(apiHealthCheckIntervalMs);
        }
    }
    if (attempts == apiHealthCheckAttempts)
        throw new Exception($"{applicationName} unhealthy after {apiHealthCheckAttempts} attempts");
    Information($"{applicationName} healthy");
}
async Task<(bool Success, string Status)> CheckHttpHealth(string uri)
{   
    try
    {
        var httpResponse = await client.GetAsync(uri);
        return (httpResponse.IsSuccessStatusCode, httpResponse.StatusCode.ToString());
    }
    catch (Exception ex)
    {
        return (false, ex.Message);
    }
}
