#region Build

Task("Clean")
    .Does<BuildParameters>(parameters =>
    {
        Information("Cleaning Directories...");

        // Clean src
        CleanDirectories("./src/**/bin/" + parameters.Configuration);
        CleanDirectories("./src/**/obj");

        // Clean test
        CleanDirectories("./test/**/bin/" + parameters.Configuration);
        CleanDirectories("./test/**/obj");

        CleanDirectories(parameters.Paths.Directories.ToClean);
    });

Task("Build")
    .IsDependentOn("Clean")
    .Does<BuildParameters>(parameters =>
    {
        // Build .NET Code
        var sln = "./DacTools.sln";
        DotNetCoreRestore(sln, new DotNetCoreRestoreSettings
        {
            Verbosity = DotNetCoreVerbosity.Minimal,
            Sources = new [] { "https://api.nuget.org/v3/index.json" },
            MSBuildSettings = parameters.MSBuildSettings
        });

        var slnPath = MakeAbsolute(new DirectoryPath(sln));
        DotNetCoreBuild(slnPath.FullPath, new DotNetCoreBuildSettings
        {
            Verbosity = DotNetCoreVerbosity.Minimal,
            Configuration = parameters.Configuration,
            NoRestore = true,
            MSBuildSettings = parameters.MSBuildSettings
        });
    });

#endregion

#region Tests

Task("Test")
    .WithCriteria<BuildParameters>((context, parameters) => parameters.EnabledUnitTests, "Unit Tests were disabled.")
    .IsDependentOn("Build")
    .Does<BuildParameters>(parameters =>
    {
        var frameworks = new List<string> { parameters.CoreFxVersion21, parameters.CoreFxVersion30 };
        if (parameters.IsRunningOnWindows)
            frameworks.Add(parameters.FullFxVersion472);

        var testResultsPath = parameters.Paths.Directories.TestResultsOutput;

        foreach (var framework in frameworks)
        {
            // run using dotnet test
            var actions = new List<Action>();
            var projects = GetFiles("./test/**/*.Tests.csproj");

            foreach (var project in projects)
            {
                actions.Add(() =>
                {
                    var projectName = $"{project.GetFilenameWithoutExtension()}.{framework}";
                    var settings = new DotNetCoreTestSettings
                    {
                        Framework = framework,
                        NoBuild = true,
                        NoRestore = true,
                        Configuration = parameters.Configuration
                    };

                    if (!parameters.IsRunningOnMacOS)
                    {
                        settings.TestAdapterPath = new DirectoryPath(".");
                        var resultsPath = MakeAbsolute(testResultsPath.CombineWithFilePath($"{projectName}.results.xml"));
                        settings.Logger = $"trx;LogFileName={resultsPath}";
                    }

                    DotNetCoreTest(project.FullPath, settings);
                });
            }

            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = -1,
                CancellationToken = default
            };

            Parallel.Invoke(options, actions.ToArray());
        }
    })
    .ReportError(exception =>
    {
        var error = (exception as AggregateException).InnerExceptions[0];
        Error(error.Dump());
    })
    .Finally(() =>
    {
        var parameters = Context.Data.Get<BuildParameters>();
        var testResultsFiles = GetFiles(parameters.Paths.Directories.TestResultsOutput + "/*.results.xml");

        if (parameters.IsRunningOnAzurePipelines)
        {
            if (testResultsFiles.Any())
            {
                var data = new TFBuildPublishTestResultsData
                {
                    TestRunTitle = $"Tests_{parameters.Configuration}_{parameters.PlatformFamily.ToString()}",
                    TestResultsFiles = testResultsFiles.ToArray(),
                    TestRunner = TFTestRunnerType.VSTest
                };

                TFBuild.Commands.PublishTestResults(data);
            }
        }
    });

#endregion

#region Pack

Task("Pack-Prepare")
    .IsDependentOn("Test")
    .Does<BuildParameters>(parameters =>
    {
        // Publish Single File for all Native Runtimes (self-contained)
        foreach (var runtime in parameters.NativeRuntimes)
        {
            var runtimeName = runtime.Value;

            var settings = new DotNetCorePublishSettings
            {
                Framework = parameters.CoreFxVersion30,
                Runtime = runtimeName,
                NoRestore = false,
                Configuration = parameters.Configuration,
                OutputDirectory = parameters.Paths.Directories.Native.Combine(runtimeName),
                MSBuildSettings = parameters.MSBuildSettings
            };

            settings.ArgumentCustomization =
                arg => arg
                .Append("/p:PublishSingleFile=true")
                .Append("/p:IncldueSymbolsInSingleFile=true");

            DotNetCorePublish("./src/DacTools.Deployment/DacTools.Deployment.csproj", settings);
        }

        var frameworks = new List<string> { parameters.CoreFxVersion21, parameters.CoreFxVersion30 };
        if (parameters.IsRunningOnWindows)
            frameworks.Add(parameters.FullFxVersion472);

        // Publish Framework-Dependent Deployment
        foreach (var framework in frameworks)
        {
            var settings = new DotNetCorePublishSettings
            {
                Framework = framework,
                NoRestore = false,
                Configuration = parameters.Configuration,
                OutputDirectory = parameters.Paths.Directories.ArtifactsBin.Combine(framework),
                MSBuildSettings = parameters.MSBuildSettings
            };

            DotNetCorePublish("./src/DacTools.Deployment/DacTools.Deployment.csproj", settings);
        }
    });

Task("Zip-Files")
    .IsDependentOn("Pack-Prepare")
    .Does<BuildParameters>(parameters =>
    {
        foreach (var runtime in parameters.NativeRuntimes)
        {
            var sourceDir = parameters.Paths.Directories.Native.Combine(runtime.Value);
            var fileName = $"dactools-deployment-{runtime.Key}-{parameters.Version.SemVersion}.tar.gz".ToLower();
            var tarFile = parameters.Paths.Directories.Artifacts.CombineWithFilePath(fileName);
            GZipCompress(sourceDir, tarFile);
        }
    });

#endregion
