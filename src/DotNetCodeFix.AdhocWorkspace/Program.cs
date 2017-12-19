using System;
using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;

namespace DotNetCodeFix.AdhocWorkspace
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var workspace = new Microsoft.CodeAnalysis.AdhocWorkspace();
            workspace.WorkspaceFailed += (s, e) => { Console.WriteLine(e.Diagnostic); };

            var projName = "NewProject";
            var projectId = ProjectId.CreateNewId();
            var versionStamp = VersionStamp.Create();
            var projectInfo = ProjectInfo.Create(projectId, versionStamp, projName, projName, LanguageNames.CSharp);
            var newProject = workspace.AddProject(projectInfo);

            var ns = new[] {
                "System",
                "System.IO"
             };

            var code = @"
using System; 
using System.Threading.Tasks;
class A { 
    static void Main(string[] args) { } 
    static async Task B() {  
        await Task.Run(() => {});
    } 
}
             ";

            var sourceText = SourceText.From(code);

            var newDocument = workspace.AddDocument(newProject.Id, "NewFile.cs", sourceText);
            //var documentA = newProject.AddDocument("A", sourceText);

            var analyzers = ImmutableArray.CreateBuilder<DiagnosticAnalyzer>();
            Assembly.GetAssembly(typeof(AsyncMethodWithoutAsyncSuffixAnalyzer))
                .GetTypes()
                .Where(typeof(DiagnosticAnalyzer).IsAssignableFrom)
                .Select(Activator.CreateInstance)
                .Cast<DiagnosticAnalyzer>()
                .ToList()
            .ForEach(analyzers.Add);

            foreach (var doc in newProject.Documents)
            {
                Console.WriteLine($"Document: {doc.Name}");
            }

            CSharpCompilationOptions DefaultCompilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
               .WithOverflowChecks(true)
               .WithPlatform(Platform.X86)
               .WithOptimizationLevel(OptimizationLevel.Release)
               .WithUsings(ns);

            var DefaultReferences = new[] {
                    MetadataReference.CreateFromFile(typeof (object).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof (System.Linq.Enumerable).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof (System.GenericUriParser).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException).Assembly.Location)
            };

            var compilation = CSharpCompilation.Create("Hello", new[] { newDocument.GetSyntaxTreeAsync().Result }).WithReferences(DefaultReferences);

            //var compilation = await newProject.GetCompilationAsync();
            foreach (var analyzer in analyzers)
            {
                var diagnosticResults = await compilation.WithAnalyzers(ImmutableArray.Create(analyzer)).GetAllDiagnosticsAsync();
                var interestingResults = diagnosticResults.ToArray();
                if (interestingResults.Any())
                {
                    Console.WriteLine($"Results for analyzer: {analyzer}");
                }

                foreach (var diagnostic in interestingResults)
                {
                    if (diagnostic.Severity != DiagnosticSeverity.Hidden)
                    {
                        Console.WriteLine($"Severity: {diagnostic.Severity} {diagnostic.Location} {string.Join(", ", diagnostic.Properties)}");
                        Console.WriteLine($"Message: {diagnostic.GetMessage()}");
                    }
                }
            }
        }
    }
}
