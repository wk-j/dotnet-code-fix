using System;
using System.Collections.Immutable;
using System.Reflection;
using Buildalyzer;
using Buildalyzer.Workspaces;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeFixes;
using System.Threading;
using Microsoft.CodeAnalysis.CodeActions;
using System.Collections.Generic;

namespace DotNetCodeFix.Buildalyzer {
    class Program {

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

        static async Task Fix(Project project) {
            var analizers = CreateAnalyzer();
            foreach (var analizer in analizers) {
                var compilation = await project.GetCompilationAsync();
                var digs = await compilation.WithAnalyzers(ImmutableArray.Create(analizer)).GetAllDiagnosticsAsync();


                foreach (var dig in digs) {
                    var actions = new List<CodeAction>();
                    foreach (var document in project.Documents) {
                        var context = new CodeFixContext(document, dig, (c, d) => {
                            actions.Add(c);
                        }, CancellationToken.None);

                        var provider = new AsyncMethodWithoutAsyncSuffixCodeFix();
                        provider.RegisterCodeFixesAsync(context).Wait();

                        //Console.WriteLine($"Action = {actions.Count}");

                        foreach (var action in actions) {
                            var operations = await action.GetOperationsAsync(CancellationToken.None);
                            foreach (var operation in operations.OfType<ApplyChangesOperation>()) {
                                var solution = operation.ChangedSolution;
                                var newDoc = solution.GetDocument(document.Id);
                                Console.WriteLine(await newDoc.GetTextAsync());
                            }
                        }
                    }

                    //Console.WriteLine($"Mesasge = {dig.GetMessage()}");
                    //Console.WriteLine($"Line = {dig.Location.GetMappedLineSpan()}");
                    //Console.WriteLine($"Path = {dig.Location.SourceTree.FilePath}");
                    Console.WriteLine();
                }
            }
        }

        static async Task Main(string[] args) {
            var projectPath = "/Users/wk/Source/DotNetCodeFix/tests/MyApp/MyApp.csproj";
            var manager = new AnalyzerManager();
            var project = manager.GetProject(projectPath);
            var sources = project.GetSourceFiles();

            var workspace = new AdhocWorkspace();
            var roslynProject = project.AddToWorkspace(workspace, true);

            await Fix(roslynProject);
        }
    }
}