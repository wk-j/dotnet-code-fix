using Buildalyzer;
using Microsoft.CodeAnalysis;
using Buildalyzer.Workspaces;

namespace DotNetCodeFix {

    class Program {
        static void Main(string[] args) {
            var projectPath = "/Users/wk/GitHub/DotNetCodeFix/tests/MyApp/MyApp.csproj";
            var manager = new AnalyzerManager();
            var project = manager.GetProject(projectPath);

            var workspace = new AdhocWorkspace();
            var roslyn = project.AddToWorkspace(workspace, true);

            var analizers = Utlity.GetAllAnalyzers();
            var codeFixers = Utlity.GetAllFixers();

            var diagnostics = Utlity.GetProjectAnalyzerDiagnosticsAsync(roslyn, analizers);
        }
    }
}
