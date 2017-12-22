using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Linq;
using Microsoft.CodeAnalysis.CodeFixes;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Serilog.Core;
using Buildalyzer;
using Buildalyzer.Workspaces;
using Microsoft.CodeAnalysis.CodeActions;

namespace DotNetCodeFix {
    public class Utlity {

        static Logger Logger = Log.New();

        public static (Workspace, Project) CreateRoslynProject(string path) {
            var manager = new AnalyzerManager();
            var project = manager.GetProject(path);
            var workspace = new AdhocWorkspace();
            var roslyn = project.AddToWorkspace(workspace, true);
            return (workspace, roslyn);
        }

        public static ImmutableArray<DiagnosticAnalyzer> GetAllAnalyzers() {
            var assembly = typeof(Utlity).Assembly;
            Logger.Information("get all analyzer - {0}", assembly.Location);

            var type = typeof(DiagnosticAnalyzer);
            var builder = ImmutableArray.CreateBuilder<DiagnosticAnalyzer>();
            var types = assembly.GetTypes().Where(type.IsAssignableFrom).Select(x => (DiagnosticAnalyzer)Activator.CreateInstance(x));
            builder.AddRange(types);

            Logger.Information("count - {0}", builder.Count);
            return builder.ToImmutable();
        }

        public static ImmutableDictionary<string, ImmutableList<CodeFixProvider>> GetAllFixers() {
            var assmbly = typeof(Utlity).Assembly;
            Logger.Information("get all fixers - {0}", assmbly.Location);

            var type = typeof(CodeFixProvider);
            var dict = new Dictionary<string, ImmutableList<CodeFixProvider>>();
            var types = assmbly.GetTypes().Where(type.IsAssignableFrom).Select(x => (CodeFixProvider)Activator.CreateInstance(x));

            foreach (var fixer in types) {
                foreach (var id in fixer.FixableDiagnosticIds) {
                    dict.AddToInnerList(id, fixer);
                }
            }

            Logger.Information("count - {0}", dict.Count);
            return dict.ToImmutableDictionary();
        }

        public static (Solution, Document) Fix(Document document, Diagnostic dig, CodeFixProvider fixer) {
            var actions = new List<CodeAction>();
            var context = new CodeFixContext(document, dig, (c, d) => {
                Logger.Information("CodeAction = {C}", c);
                Logger.Information("Diagnositc = {D}", d);
                actions.Add(c);
            }, CancellationToken.None);

            fixer.RegisterCodeFixesAsync(context).Wait();

            var operations = actions[0].GetOperationsAsync(CancellationToken.None).Result;
            var solution = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;
            var newDocument = solution.GetDocument(document.Id);
            return (solution, newDocument);
        }


        public static async Task<ImmutableArray<Diagnostic>> GetProjectAnalyzerDiagnosticsAsync(Project project, ImmutableArray<DiagnosticAnalyzer> analzyers) {
            var cancel = new CancellationTokenSource();
            var supportedDiagnosticsSpecificOptions = new Dictionary<string, ReportDiagnostic>();
            analzyers.Select(x => x.SupportedDiagnostics).SelectMany(x => x).ToList().ForEach(x => {
                supportedDiagnosticsSpecificOptions[x.Id] = ReportDiagnostic.Default;
            });

            supportedDiagnosticsSpecificOptions.Add("AD001", ReportDiagnostic.Error);
            var modifiedSpecificDiagnosticOptions = supportedDiagnosticsSpecificOptions.ToImmutableDictionary().SetItems(project.CompilationOptions.SpecificDiagnosticOptions);
            var modifiedCompilationOptions = project.CompilationOptions.WithSpecificDiagnosticOptions(modifiedSpecificDiagnosticOptions);
            var compilation = await project.GetCompilationAsync(cancel.Token).ConfigureAwait(false);
            var compalationWithAnalyzers = compilation.WithAnalyzers(analzyers, cancellationToken: cancel.Token);
            var diagnostics = await compalationWithAnalyzers.GetAllDiagnosticsAsync();

            Logger.Information("get diagnostics - {0}", project.FilePath);
            Logger.Information("count - {0}", diagnostics.Count());

            return diagnostics;
        }
    }
}
