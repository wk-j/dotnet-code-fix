using System;

using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.MSBuild;
using System.Threading.Tasks;
using System.Collections.Immutable;
using System.Reflection;
using System.Linq;
using Microsoft.CodeAnalysis;
using System.Threading;

namespace DotNetCodeFix
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Environment.SetEnvironmentVariable("MSBUILD_EXE_PATH", "/Library/Frameworks/Mono.framework/Versions/Current/Commands/msbuild");
            Environment.SetEnvironmentVariable("MSBUILD_EXE_PATH", "/usr/local/share/dotnet/sdk/2.1.2/MSBuild.dll");

            Environment.SetEnvironmentVariable("COREHOST_TRACE", "1");

            Environment.SetEnvironmentVariable("MSBuildSDKsPath", "/usr/local/share/dotnet/sdk/2.1.2/Sdks");
            // Environment.SetEnvironmentVariable("MSBuildSDKsPath", "/Library/Frameworks/Mono.framework/Versions/Current/lib/mono/msbuild/15.0/bin/Sdks");


            var workspace = MSBuildWorkspace.Create();
            workspace.WorkspaceFailed += (s, e) => { Console.WriteLine(e.Diagnostic); };

            var project = await workspace.OpenProjectAsync("/Users/wk/Source/github/DotNetCodeFix/tests/MyApp/MyApp.csproj");
            //var project = await workspace.OpenProjectAsync("/Users/wk/Source/project/standard/easy-capture/EasyCapture/EasyCapture.csproj");

            Console.WriteLine($"Documents: {project.Documents.Count()}");
            Console.WriteLine($"AssemblyName: {project.AssemblyName}");

            var analyzers = ImmutableArray.CreateBuilder<DiagnosticAnalyzer>();
            Assembly.GetAssembly(typeof(AsyncMethodWithoutAsyncSuffixAnalyzer))
                .GetTypes()
                .Where(typeof(DiagnosticAnalyzer).IsAssignableFrom)
                .Select(Activator.CreateInstance)
                .Cast<DiagnosticAnalyzer>()
                .ToList()
                .ForEach(analyzers.Add);

            Console.WriteLine($"Found {analyzers.Count()}");

            var compilation = await project.GetCompilationAsync();

            var asm = compilation.AssemblyName;
            Console.WriteLine($"AssemblyName: {asm}");

            var allTypes = compilation.Assembly.TypeNames;
            Console.WriteLine($"Types: {string.Join(", ", allTypes)}");

            foreach (var analyzer in analyzers)
            {
                var diagnosticResults = await compilation.WithAnalyzers(ImmutableArray.Create(analyzer)).GetAllDiagnosticsAsync();
                //var interestingResults = diagnosticResults.Where(x => x.Severity != DiagnosticSeverity.Hidden).ToArray();
                var interestingResults = diagnosticResults.ToArray();
                if (interestingResults.Any())
                {
                    Console.WriteLine($"Results for analyzer: {analyzer}");
                }

                foreach (var diagnostic in interestingResults)
                {
                    if (diagnostic.Severity != DiagnosticSeverity.Hidden)
                    {
                        Console.WriteLine($"Severity: {diagnostic.Severity}");
                        Console.WriteLine($"Message: {diagnostic.GetMessage()}");
                    }
                }
            }
        }
    }
}
