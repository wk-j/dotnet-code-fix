using System.Collections.Immutable;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CodeActions;
using System.Threading;
using Microsoft.CodeAnalysis.Rename;

namespace DotNetCodeFix.Fixers {


    [ExportCodeFixProvider(nameof(AsyncMethodWithoutAsyncSuffixCodeFix), LanguageNames.CSharp), Shared]
    public class AsyncMethodWithoutAsyncSuffixCodeFix : CodeFixProvider {
        string title = "FixAsync";

        public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(AsyncMethodWithoutAsyncSuffixAnalyzer.Rule.Id);

        public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public override async Task RegisterCodeFixesAsync(CodeFixContext context) {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            if (!context.Diagnostics.Any()) return;

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;
            var methodDeclarations = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<MethodDeclarationSyntax>();

            if (!methodDeclarations.Any()) return;

            var methodDeclaration = methodDeclarations.First();

            // context.RegisterCodeFix(CodeAction.Create(VSDiagnosticsResources.AsyncMethodWithoutAsyncSuffixCodeFixTitle, x => AddSuffixAsync(context.Document, methodDeclaration, context.CancellationToken), nameof(AsyncMethodWithoutAsyncSuffixAnalyzer)), diagnostic);
            context.RegisterCodeFix(CodeAction.Create(title, x => AddSuffixAsync(context.Document, methodDeclaration, context.CancellationToken), nameof(AsyncMethodWithoutAsyncSuffixAnalyzer)), diagnostic);
        }

        private async Task<Solution> AddSuffixAsync(Document document, MethodDeclarationSyntax methodDeclaration, CancellationToken cancellationToken) {
            var methodSymbol = (await document.GetSemanticModelAsync(cancellationToken)).GetDeclaredSymbol(methodDeclaration);
            return await Renamer.RenameSymbolAsync(
                document.Project.Solution,
                methodSymbol,
                methodDeclaration.Identifier.Text + "Async",
                document.Project.Solution.Workspace.Options,
                cancellationToken);
        }
    }
}