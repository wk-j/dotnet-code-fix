using Buildalyzer;
using Microsoft.CodeAnalysis;
using Buildalyzer.Workspaces;
using System.Linq;
using Serilog.Core;
using DotNetCodeFix.Fixers;
using System.IO;

namespace DotNetCodeFix {

    class Program {

        static Logger logger = Log.New();

        static async void Fix1(string projectPath) {

            var id = nameof(AsyncMethodWithoutAsyncSuffixAnalyzer);

            var (wk, project) = Utlity.CreateRoslynProject(projectPath);

            wk.WorkspaceFailed += (e, a) => {
                logger.Error(a.Diagnostic.Message);
            };

            var analizers = Utlity.GetAllAnalyzers();
            var fixers = Utlity.GetAllFixers();
            var diagnostics = await Utlity.GetProjectAnalyzerDiagnosticsAsync(project, analizers);

            var fixer = fixers.First().Value.First();

            var digs = diagnostics.Where(x => x.Id == id);
            if (digs.Any()) {
                var dig = digs.First();
                var path = dig.Location.SourceTree.FilePath;
                var document = project.Documents.First(x => x.FilePath == path);
                var (sln, newDocument) = Utlity.Fix(document, dig, fixer);
                var text = await newDocument.GetTextAsync();
                using (var writer = File.CreateText(path)) {
                    text.Write(writer);
                }
            }
        }

        static void Main(string[] args) {
            if (args.Length != 1) return;
            var projectPath = args[0];
            Fix1(projectPath);
        }
    }
}