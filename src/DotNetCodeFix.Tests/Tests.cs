using System;
using Xunit;
using System.Linq;

using DotNetCodeFix;
using System.Threading.Tasks;

namespace DotNetCodeFix.Tests {
    public class Tests {
        [Fact]
        public void GetAnalizers() {
            var analyzers = Utlity.GetAllAnalyzers();
            Assert.True(analyzers.Count() > 0);
        }

        [Fact]
        public void GetCodeFizers() {
            var fixers = Utlity.GetAllFixers();
            Assert.True(fixers.Count() > 0);
        }

        [Fact]
        public async Task GetDiagnostic() {
            var projectPath = "/Users/wk/GitHub/DotNetCodeFix/tests/MyApp/MyApp.csproj";
            var project = Utlity.CreateRoslynProject(projectPath);

            var analizers = Utlity.GetAllAnalyzers();
            var diagnostics = await Utlity.GetProjectAnalyzerDiagnosticsAsync(project, analizers);
            Assert.True(diagnostics.Count() > 0);

            foreach (var dig in diagnostics) {
                Console.WriteLine(dig.Id);
                Console.WriteLine(dig.Location);
                Console.WriteLine(dig.GetMessage());
                Console.WriteLine();
            }
        }
    }
}
