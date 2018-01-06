using System;
using Xunit;
using System.Linq;

using DotNetCodeFix;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.IO;
using DotNetCodeFix.Fixers;
using Serilog.Core;

namespace DotNetCodeFix.Tests {
    public class Tests {

        string projectPath = "/Users/wk/Source/DotNetCodeFix/tests/MyApp/MyApp.csproj";
        Logger logger = Log.New();

        // [Fact]
        public void GetAnalizers() {
            var analyzers = Utlity.GetAllAnalyzers();
            Assert.True(analyzers.Count() > 0);
        }

        // [Fact]
        public void GetCodeFizers() {
            var fixers = Utlity.GetAllFixers();
            Assert.True(fixers.Count() > 0);
        }

        // [Fact]
        public async Task GetDiagnostic() {
            var (wk, project) = Utlity.CreateRoslynProject(projectPath);

            var analizers = Utlity.GetAllAnalyzers();
            var diagnostics = await Utlity.GetProjectAnalyzerDiagnosticsAsync(project, analizers);
            Assert.True(diagnostics.Count() > 0);

            foreach (var dig in diagnostics) {
                logger.Information(dig.Id);
                logger.Information(dig.Location.ToString());
                logger.Information(dig.GetMessage());
            }
        }

        [Fact]
        public void AddDocument() {
            var (workspace, project) = Utlity.CreateRoslynProject(projectPath);
            var wk = project.Solution.Workspace;
            var document = project.AddDocument("Readme.txt", "Hello!");
            var solution = document.Project.Solution;
            var rs = wk.TryApplyChanges(solution);
            Assert.Equal(true, rs);
        }

        //[Fact]
        public async Task ApplyChanges() {
            var (workspace, project) = Utlity.CreateRoslynProject(projectPath);
            workspace.WorkspaceFailed += (a, e) => {
                logger.Error(e.ToString());
            };

            var doc = project.Documents.First();
            var text = await doc.GetTextAsync();

            logger.Information("Path = {0}", doc.FilePath);

            var oldSolution = workspace.CurrentSolution;

            var sourceText = SourceText.From(text + "\naaaa");
            var newSolution = oldSolution.WithDocumentText(doc.Id, sourceText);
            var rs = workspace.TryApplyChanges(newSolution);
            Assert.Equal(true, rs);
        }


        //[Fact]
        public async Task Fix() {

            var id = nameof(AsyncMethodWithoutAsyncSuffixAnalyzer);

            var (wk, project) = Utlity.CreateRoslynProject(projectPath);

            wk.WorkspaceFailed += (e, a) => {
                logger.Error(a.Diagnostic.Message);
            };

            var analizers = Utlity.GetAllAnalyzers();
            var fixers = Utlity.GetAllFixers();
            var diagnostics = await Utlity.GetProjectAnalyzerDiagnosticsAsync(project, analizers);

            var fixer = fixers.First().Value.First();

            foreach (var dig in diagnostics.Where(x => x.Id == id)) {
                var path = dig.Location.SourceTree.FilePath;
                var document = project.Documents.First(x => x.FilePath == path);

                var text = File.ReadAllText(path);
                var (sln, newDocument) = Utlity.Fix(document, dig, fixer);

                logger.Information("solution = {Sln}", sln);

                var ok = wk.TryApplyChanges(sln);
                Assert.Equal(true, ok);
            }
        }

        [Fact]
        public async Task Fix2() {

            var id = nameof(AsyncMethodWithoutAsyncSuffixAnalyzer);

            var (wk, project) = Utlity.CreateRoslynProject(projectPath);

            wk.WorkspaceFailed += (e, a) => {
                logger.Error(a.Diagnostic.Message);
            };

            var analizers = Utlity.GetAllAnalyzers();
            var fixers = Utlity.GetAllFixers();
            var diagnostics = await Utlity.GetProjectAnalyzerDiagnosticsAsync(project, analizers);

            var fixer = fixers.First().Value.First();

            foreach (var dig in diagnostics.Where(x => x.Id == id)) {
                var path = dig.Location.SourceTree.FilePath;
                var document = project.Documents.First(x => x.FilePath == path);

                var text = File.ReadAllText(path);
                var (sln, newDocument) = Utlity.Fix(document, dig, fixer);
                Console.WriteLine(newDocument.GetTextAsync().Result);
            }
        }
    }
}
