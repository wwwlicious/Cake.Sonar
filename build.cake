#addin "Cake.Git"

/// what came first, chicken or egg ^^
// #addin "nuget:?package=Cake.Sonar"
#tool "nuget:?package=MSBuild.SonarQube.Runner.Tool"


var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");

var solution = "./src/Cake.Sonar.sln";

var appName = "Cake.Sonar";

///////////////////////////////////////////////////////////////////////////////
// WAZZUP
///////////////////////////////////////////////////////////////////////////////

var local = BuildSystem.IsLocalBuild;
var isRunningOnAppVeyor = AppVeyor.IsRunningOnAppVeyor;
var isPullRequest = AppVeyor.Environment.PullRequest.IsPullRequest;
var buildNumber = AppVeyor.Environment.Build.Number;


var branchName = isRunningOnAppVeyor ? EnvironmentVariable("APPVEYOR_REPO_BRANCH") : GitBranchCurrent(DirectoryPath.FromString(".")).FriendlyName;
var isMasterBranch = System.String.Equals("master", branchName, System.StringComparison.OrdinalIgnoreCase);

///////////////////////////////////////////////////////////////////////////////
// VERSION
///////////////////////////////////////////////////////////////////////////////

var version = "1.0.0";
var semVersion = local ? version : (version + string.Concat("+", buildNumber));

///////////////////////////////////////////////////////////////////////////////
// PREPARE
///////////////////////////////////////////////////////////////////////////////

Task("Clean")
	.Does(() => {
		CleanDirectory("./nuget");
	});

Task("Restore-Nuget-Packages")
	.IsDependentOn("Clean")
	.Does(() => {
		NuGetRestore(solution);
	});

//////////////////////////////////////////////////////////////////////////////
// Build
//////////////////////////////////////////////////////////////////////////////

Task("Build")
	.IsDependentOn("Restore-Nuget-Packages")
	.Does(() => {
		MSBuild(solution,new MSBuildSettings {
    		Verbosity = Verbosity.Minimal,
    		Configuration = configuration
    	});
	});

//////////////////////////////////////////////////////////////////////////////
// Nuget
//////////////////////////////////////////////////////////////////////////////

Task("Pack")
	.IsDependentOn("Build")
	.Does(() => {
		
		CreateDirectory("nuget");
		CleanDirectory("nuget");
		
		var nuGetPackSettings   = new NuGetPackSettings {
                                Id                      = appName,
                                Version                 = version,
                                Title                   = appName,
                                Authors                 = new[] {"Tom Staijen"},
                                Owners                  = new[] {"Tom Staijen", "cake-contrib"},
                                Description             = "Run sonar for msbuild",
                                Summary                 = "Run sonar for msbuild",
								IconUrl					= new Uri("https://cdn.rawgit.com/cake-contrib/graphics/a5cf0f881c390650144b2243ae551d5b9f836196/png/cake-contrib-medium.png"),
                                ProjectUrl              = new Uri("https://github.com/AgileArchitect/Cake.Sonar"),
								LicenseUrl              = new Uri("https://github.com/AgileArchitect/Cake.Sonar/blob/master/LICENCE"),
                                Tags                    = new [] {"Cake", "Sonar", "MSBuild"},
                                RequireLicenseAcceptance= false,
                                Symbols                 = false,
                                NoPackageAnalysis       = true,
                                Files                   = new [] {
                                                                     new NuSpecContent {Source = "net45/Cake.Sonar.dll", Target = "lib/net45" },
                                                                     new NuSpecContent {Source = "net45/Cake.Sonar.xml", Target = "lib/net45" },
																	 new NuSpecContent {Source = "net46/Cake.Sonar.dll", Target = "lib/net46" },
                                                                     new NuSpecContent {Source = "net46/Cake.Sonar.xml", Target = "lib/net46" },
																	 new NuSpecContent {Source = "netstandard1.6/Cake.Sonar.dll", Target = "lib/netstandard1.6" },
                                                                     new NuSpecContent {Source = "netstandard1.6/Cake.Sonar.xml", Target = "lib/netstandard1.6" }
                                                                  },
                                BasePath                = "./src/Cake.Sonar/bin/release",
                                OutputDirectory         = "./nuget"
                            };
            
		NuGetPack(nuGetPackSettings);
	});

Task("Publish")
	.IsDependentOn("Pack")
    .WithCriteria(() => isRunningOnAppVeyor)
    .WithCriteria(() => !isPullRequest)
    .WithCriteria(() => isMasterBranch)
	.Does(() => {
		
	    var apiKey = EnvironmentVariable("NUGET_API_KEY");

    	if(string.IsNullOrEmpty(apiKey))    
        	throw new InvalidOperationException("Could not resolve Nuget API key.");
		
		var package = "./nuget/Cake.Sonar." + version + ".nupkg";
            
		// Push the package.
		NuGetPush(package, new NuGetPushSettings {
    		Source = "https://www.nuget.org/api/v2/package",
    		ApiKey = apiKey
		});
	});

///////////////////////////////////////////////////////////////////////////////
// APPVEYOR
///////////////////////////////////////////////////////////////////////////////

Task("Update-AppVeyor-Build-Number")
    .WithCriteria(() => isRunningOnAppVeyor)
    .Does(() =>
{
    AppVeyor.UpdateBuildVersion(semVersion);
});


// Task("Analyse")
// 	.IsDependentOn("Clean")
// 	.Does(() => {
// 		var settings = new SonarBeginSettings() {
// 			Url = EnvironmentVariable("SONAR_URL"),
// 			Key = "Cake.Sonar",
// 		};

// 		Sonar(
// 			ctx => {
// 				ctx.MSBuild(solution);
// 			}, settings
// 		);
// 	});

Task("AppVeyor")
	.IsDependentOn("Update-AppVeyor-Build-Number")
	.IsDependentOn("Publish");
	

///////////////////////////////////////////////////////////////////////////////
// EXECUTION
///////////////////////////////////////////////////////////////////////////////

Task("Default")
	.IsDependentOn("Pack");

RunTarget(target);
