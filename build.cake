var name = "DotNetCodeFix";

var project = $"src/{name}/{name}.csproj";

Task("Zip")
    .IsDependentOn("Publish")
    .Does(() => {
        Zip($"publish/dotnet-code-fix", "publish/dotnet-code-fix.0.1.0.zip");
    });


Task("Publish").Does(() => {
    DotNetCorePublish(project, new DotNetCorePublishSettings {
        OutputDirectory = "publish/dotnet-core-fix"
    });
});

var target = Argument("target", "default");
RunTarget(target);