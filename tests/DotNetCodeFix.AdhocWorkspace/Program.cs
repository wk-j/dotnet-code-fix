using System;
using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using System.Threading;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using System.Collections.Generic;

namespace DotNetCodeFix.AdhocWorkspaces {
    class Program {
        static (AdhocWorkspace, Project) CreateWorkspace() {
            var workspace = new Microsoft.CodeAnalysis.AdhocWorkspace();
            workspace.WorkspaceFailed += (s, e) => { Console.WriteLine(e.Diagnostic); };

            var projName = "NewProject";
            var projectId = ProjectId.CreateNewId();
            var versionStamp = VersionStamp.Create();
            var projectInfo = ProjectInfo.Create(projectId, versionStamp, projName, projName, LanguageNames.CSharp);
            var newProject = workspace.AddProject(projectInfo);
            return (workspace, newProject);
        }

        static SourceText CreateSourceText() {

            var code = @"
using System;
using System.Threading.Tasks;
class A { 
    static void Main(string[] args) { 

    } 
    static async Task B() {  
        await Task.Run(() => {
            
        });
    } 
} ";

            var sourceText = SourceText.From(code);
            return sourceText;
        }

        async Task K() {
            await Task.Run(() => {

            });
        }

        static ImmutableArray<DiagnosticAnalyzer>.Builder CreateAnalyzer() {
            var analyzers = ImmutableArray.CreateBuilder<DiagnosticAnalyzer>();
            Assembly.GetAssembly(typeof(AsyncMethodWithoutAsyncSuffixAnalyzer))
                .GetTypes()
                .Where(typeof(DiagnosticAnalyzer).IsAssignableFrom)
                .Select(Activator.CreateInstance)
                .Cast<DiagnosticAnalyzer>()
                .ToList()
            .ForEach(analyzers.Add);
            return analyzers;
        }

        static void Test1(Document document, CSharpCompilation compilation) {

            var analyzer = CreateAnalyzer().ElementAt(0);

            var all = compilation.WithAnalyzers(ImmutableArray.Create(analyzer)).GetAllDiagnosticsAsync().Result;
            var first = all.Skip(1).First();
            var span = first.Location.SourceSpan;

            Console.WriteLine($"All = {string.Join(",", all.Select(x => x.GetMessage()))}");
            Console.WriteLine($"First = {first}");
            Console.WriteLine($"Span = {span}");

            var actions = new List<CodeAction>();

            var context = new CodeFixContext(document, first, (c, d) => {
                Console.WriteLine(c);
                Console.WriteLine(d);
                actions.Add(c);
            }, CancellationToken.None);

            var provider = new AsyncMethodWithoutAsyncSuffixCodeFix();
            provider.RegisterCodeFixesAsync(context).Wait();

            var operations = actions[0].GetOperationsAsync(CancellationToken.None).Result;
            var solution = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;
            var newDoc = solution.GetDocument(document.Id);

            Console.WriteLine(newDoc.GetTextAsync().Result);
        }

        static async Task Main(string[] args) {
            var (workspace, project) = CreateWorkspace();
            var sourceText = CreateSourceText();
            var document = workspace.AddDocument(project.Id, "NewFile.cs", sourceText);
            var analyzers = CreateAnalyzer();

            var ns = new[] {
                "System",
                "System.Threading.Tasks",
                "System.IO"
             };

            var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
               .WithOverflowChecks(true)
               .WithPlatform(Platform.X64)
               .WithUsings(ns)
               .WithOptimizationLevel(OptimizationLevel.Release);

            var references = new[] {
                    MetadataReference.CreateFromFile(typeof (object).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof (System.Linq.Enumerable).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof (System.GenericUriParser).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException).Assembly.Location)
            };

            var compilation =
                CSharpCompilation.Create("Hello.dll", new[] { await document.GetSyntaxTreeAsync() })
                .WithOptions(compilationOptions)
                .WithReferences(references);


            foreach (var analyzer in analyzers) {
                var diagnosticResults = await compilation.WithAnalyzers(ImmutableArray.Create(analyzer)).GetAllDiagnosticsAsync();
                var interestingResults = diagnosticResults.ToArray();



                foreach (var diagnostic in interestingResults) {
                    if (diagnostic.Severity != DiagnosticSeverity.Hidden) {
                        Console.WriteLine($"Severity: {diagnostic.Severity}, {diagnostic.Location}, {diagnostic.Descriptor}");
                        Console.WriteLine($"Message: {diagnostic.GetMessage()}");
                    }
                }
            }

            Test1(document, compilation);
        }
    }
}