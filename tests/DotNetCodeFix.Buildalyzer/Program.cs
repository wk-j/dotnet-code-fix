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
using static System.Console;
using System.Diagnostics;


internal static class Extensions {
    internal static void AddToInnerList<TKey, TValue>(this IDictionary<TKey, ImmutableList<TValue>> dictionary, TKey key, TValue item) {
        ImmutableList<TValue> items;

        if (dictionary.TryGetValue(key, out items)) {
            dictionary[key] = items.Add(item);
        } else {
            dictionary.Add(key, ImmutableList.Create(item));
        }
    }
}

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

    static async Task Process(Document document, Diagnostic dig) {

        var actions = new List<CodeAction>();
        var context = new CodeFixContext(document, dig, (c, d) => {
            actions.Add(c);
        }, CancellationToken.None);

        var provider = new AsyncMethodWithoutAsyncSuffixCodeFix();
        provider.RegisterCodeFixesAsync(context).Wait();

        foreach (var action in actions) {
            var operations = await action.GetOperationsAsync(CancellationToken.None);
            foreach (var operation in operations.OfType<ApplyChangesOperation>()) {
                var solution = operation.ChangedSolution;
                var newDoc = solution.GetDocument(document.Id);
            }
        }
    }

    private static ImmutableDictionary<string, ImmutableList<CodeFixProvider>> GetAllCodeFixers() {
        Assembly assembly = typeof(Program).Assembly;

        var codeFixProviderType = typeof(CodeFixProvider);

        Dictionary<string, ImmutableList<CodeFixProvider>> providers = new Dictionary<string, ImmutableList<CodeFixProvider>>();

        foreach (var type in assembly.GetTypes()) {
            if (type.IsSubclassOf(codeFixProviderType) && !type.IsAbstract) {
                var codeFixProvider = (CodeFixProvider)Activator.CreateInstance(type);
                foreach (var diagnosticId in codeFixProvider.FixableDiagnosticIds) {
                    providers.AddToInnerList(diagnosticId, codeFixProvider);
                }
            }
        }

        return providers.ToImmutableDictionary();
    }

    /*
    private static async Task TestFixAllAsync(Stopwatch stopwatch, Solution solution, ImmutableDictionary<ProjectId, ImmutableArray<Diagnostic>> diagnostics, CancellationToken cancellationToken) {
        Console.WriteLine("Calculating fixes");

        var codeFixers = GetAllCodeFixers().SelectMany(x => x.Value).Distinct();

        var equivalenceGroups = new List<CodeFixEquivalenceGroup>();

        foreach (var codeFixer in codeFixers) {
            equivalenceGroups.AddRange(await CodeFixEquivalenceGroup.CreateAsync(codeFixer, diagnostics, solution, cancellationToken).ConfigureAwait(true));
        }

        Console.WriteLine($"Found {equivalenceGroups.Count} equivalence groups.");

        Console.WriteLine("Calculating changes");

        foreach (var fix in equivalenceGroups) {
            try {
                stopwatch.Restart();
                Console.WriteLine($"Calculating fix for {fix.CodeFixEquivalenceKey} using {fix.FixAllProvider} for {fix.NumberOfDiagnostics} instances.");
                await fix.GetOperationsAsync(cancellationToken).ConfigureAwait(true);
                WriteLine($"Calculating changes completed in {stopwatch.ElapsedMilliseconds}ms. This is {fix.NumberOfDiagnostics / stopwatch.Elapsed.TotalSeconds:0.000} instances/second.", ConsoleColor.Yellow);
            } catch (Exception ex) {
                // Report thrown exceptions
                WriteLine($"The fix '{fix.CodeFixEquivalenceKey}' threw an exception after {stopwatch.ElapsedMilliseconds}ms:", ConsoleColor.Yellow);
                WriteLine(ex.ToString(), ConsoleColor.Yellow);
            }
        }
    }
    */

    static async Task Fix(Project project) {
        var analizers = CreateAnalyzer();
        foreach (var analizer in analizers) {
            var compilation = await project.GetCompilationAsync();
            var digs = await compilation.WithAnalyzers(ImmutableArray.Create(analizer)).GetAllDiagnosticsAsync();

            var docs = project.Documents;

            foreach (var dig in digs) {
            }

            foreach (var doc in docs) {
                Console.WriteLine(await doc.GetTextAsync());
            }
        }
    }
    private static async Task<ImmutableArray<Diagnostic>> GetProjectAnalyzerDiagnosticsAsync(ImmutableArray<DiagnosticAnalyzer> analyzers, Project project, CancellationToken cancellationToken) {
        var supportedDiagnosticsSpecificOptions = new Dictionary<string, ReportDiagnostic>();
        foreach (var analyzer in analyzers) {
            foreach (var diagnostic in analyzer.SupportedDiagnostics) {
                // make sure the analyzers we are testing are enabled
                supportedDiagnosticsSpecificOptions[diagnostic.Id] = ReportDiagnostic.Default;
            }
        }

        // Report exceptions during the analysis process as errors
        supportedDiagnosticsSpecificOptions.Add("AD0001", ReportDiagnostic.Error);

        // update the project compilation options
        var modifiedSpecificDiagnosticOptions = supportedDiagnosticsSpecificOptions.ToImmutableDictionary().SetItems(project.CompilationOptions.SpecificDiagnosticOptions);
        var modifiedCompilationOptions = project.CompilationOptions.WithSpecificDiagnosticOptions(modifiedSpecificDiagnosticOptions);
        var processedProject = project.WithCompilationOptions(modifiedCompilationOptions);

        Compilation compilation = await processedProject.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
        CompilationWithAnalyzers compilationWithAnalyzers = compilation.WithAnalyzers(analyzers, cancellationToken: cancellationToken);

        //var diagnostics = await FixAllContextHelper.GetAllDiagnosticsAsync(compilation, compilationWithAnalyzers, analyzers, project.Documents, true, cancellationToken).ConfigureAwait(false);
        var diagnostics = await compilationWithAnalyzers.GetAllDiagnosticsAsync();
        return diagnostics;
    }

    private static async Task<ImmutableDictionary<ProjectId, ImmutableArray<Diagnostic>>> GetAnalyzerDiagnosticsAsync(Project project, ImmutableArray<DiagnosticAnalyzer> analyzers, CancellationToken cancellationToken) {
        List<KeyValuePair<ProjectId, Task<ImmutableArray<Diagnostic>>>> projectDiagnosticTasks = new List<KeyValuePair<ProjectId, Task<ImmutableArray<Diagnostic>>>>();

        projectDiagnosticTasks.Add(new KeyValuePair<ProjectId, Task<ImmutableArray<Diagnostic>>>(project.Id, GetProjectAnalyzerDiagnosticsAsync(analyzers, project, cancellationToken)));

        ImmutableDictionary<ProjectId, ImmutableArray<Diagnostic>>.Builder projectDiagnosticBuilder = ImmutableDictionary.CreateBuilder<ProjectId, ImmutableArray<Diagnostic>>();
        foreach (var task in projectDiagnosticTasks) {
            projectDiagnosticBuilder.Add(task.Key, await task.Value.ConfigureAwait(false));
        }

        return projectDiagnosticBuilder.ToImmutable();
    }

    private static ImmutableArray<DiagnosticAnalyzer> GetAllAnalyzers() {
        Assembly assembly = typeof(AsyncMethodWithoutAsyncSuffixAnalyzer).Assembly;

        var diagnosticAnalyzerType = typeof(DiagnosticAnalyzer);

        var analyzers = ImmutableArray.CreateBuilder<DiagnosticAnalyzer>();

        foreach (var type in assembly.GetTypes()) {
            if (type.IsSubclassOf(diagnosticAnalyzerType) && !type.IsAbstract) {
                analyzers.Add((DiagnosticAnalyzer)Activator.CreateInstance(type));
            }
        }

        return analyzers.ToImmutable();
    }

    static async Task Main(string[] args) {
        var projectPath = "/Users/wk/Source/DotNetCodeFix/tests/MyApp/MyApp.csproj";
        var manager = new AnalyzerManager();
        var project = manager.GetProject(projectPath);
        var sources = project.GetSourceFiles();

        var workspace = new AdhocWorkspace();
        var roslynProject = project.AddToWorkspace(workspace, true);

        CancellationTokenSource cts = new CancellationTokenSource();

        var analyzers = GetAllAnalyzers();
        Console.WriteLine($"{string.Join(", ", analyzers.Select(x => x.GetType().Name))}");

        var codeFixers = GetAllCodeFixers().SelectMany(x => x.Value).Distinct();
        Console.WriteLine($"{string.Join(", ", codeFixers.Select(x => x.GetType().Name))}");

        var equivalenceGroups = new List<CodeFixEquivalenceGroup>();
        var diagnostics = await GetAnalyzerDiagnosticsAsync(roslynProject, analyzers, cts.Token).ConfigureAwait(true);

        var empty = new AdhocWorkspace();
        var solution = empty.AddSolution(SolutionInfo.Create(SolutionId.CreateNewId(), VersionStamp.Create(DateTime.Now), "/Users/wk/Temp/MyApp.sln"));

        foreach (var codeFixer in codeFixers) {
            //Console.WriteLine($"{codeFixer.GetFixAllProvider().GetType().Name}");
            equivalenceGroups.AddRange(await CodeFixEquivalenceGroup.CreateAsync(codeFixer, diagnostics, solution, cts.Token).ConfigureAwait(true));
        }

        var stopwatch = new Stopwatch();


        foreach (var fix in equivalenceGroups) {
            try {
                stopwatch.Restart();
                Console.WriteLine($"Calculating fix for {fix.CodeFixEquivalenceKey} using {fix.FixAllProvider} for {fix.NumberOfDiagnostics} instances.");
                var rs = await fix.GetOperationsAsync(cts.Token).ConfigureAwait(true);
                foreach (var item in rs) {
                    Console.WriteLine($"Apply {item.Title}");
                    item.Apply(empty, cts.Token);
                }
                WriteLine($"Calculating changes completed in {stopwatch.ElapsedMilliseconds}ms. This is {fix.NumberOfDiagnostics / stopwatch.Elapsed.TotalSeconds:0.000} instances/second.", ConsoleColor.Yellow);
            } catch (Exception ex) {
                // Report thrown exceptions
                WriteLine($"The fix '{fix.CodeFixEquivalenceKey}' threw an exception after {stopwatch.ElapsedMilliseconds}ms:", ConsoleColor.Yellow);
                WriteLine(ex.ToString(), ConsoleColor.Yellow);
            }
        }
    }
}