using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.RemoveUnnecessaryImports;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Simplification;
using Microsoft.CodeAnalysis.CodeGeneration;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Formatting;
using System.Composition;
using Microsoft.CodeAnalysis.CodeRefactorings.MoveType;

namespace Microsoft.CodeAnalysis.CSharp.CodeRefactorings.MoveType
{
    [ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = "MoveType"), Shared]
    internal class MoveTypeCodeRefactoringProvider : AbstractMoveTypeCodeRefactoringProvider
    {
        //public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        //{
        //    var document = context.Document;
        //    var textSpan = context.Span;
        //    var cancellationToken = context.CancellationToken;

        //    // TODO: what checks do i need here?
        //    if (!textSpan.IsEmpty)
        //    {
        //        return;
        //    }

        //    if (document.Project.Solution.Workspace.Kind == WorkspaceKind.MiscellaneousFiles)
        //    {
        //        return;
        //    }

        //    var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        //    var typeDeclaration = root.FindNode(textSpan) as BaseTypeDeclarationSyntax;

        //    // TODO: change this, the user may want to create a partial type in another file.
        //    if (typeDeclaration == null ||
        //        string.Equals(document.Name, typeDeclaration.Identifier.ValueText, StringComparison.CurrentCultureIgnoreCase))
        //    {
        //        return;
        //    }

        //    // TODO: see IntroduceVariable to see how to register more than 1 actions.
        //    // AbstractIntroduceVariableService.cs::IntroduceVariableAsync::65
        //    context.RegisterRefactoring(
        //        new MoveTypeCodeAction(
        //            "Move type to File",
        //            (c) => MoveTypeToNewFileAsync(document, typeDeclaration, cancellationToken)));

        //}

        private async Task<Solution> MoveTypeToNewFileAsync(Document sourceDocument, BaseTypeDeclarationSyntax typeDeclaration, CancellationToken cancellationToken)
        {
            var originalSolution = sourceDocument.Project.Solution;
            var semanticModel = await sourceDocument.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var typeSymbol = semanticModel.GetDeclaredSymbol(typeDeclaration, cancellationToken);

            var declarations = typeSymbol.DeclaringSyntaxReferences;

            // compiler declared types, anonymous types, types defined in metadata should be filtered out.
            if (typeSymbol.Locations.Any(loc => loc.IsInMetadata) ||
                typeSymbol.IsAnonymousType ||
                typeSymbol.IsImplicitlyDeclared)
            {
                return originalSolution;
            }

            // TODO: deal with partial types, nested types etc.

            var sourceSyntaxTree = await sourceDocument.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);
            var sourceSyntaxRoot = await sourceSyntaxTree.GetRootAsync(cancellationToken).ConfigureAwait(false);
            var usingStatements = sourceSyntaxRoot.DescendantNodesAndSelf().Where(n => n is UsingDirectiveSyntax);

            var documentEditor = await DocumentEditor.CreateAsync(sourceDocument, cancellationToken).ConfigureAwait(false);
            documentEditor.RemoveNode(typeDeclaration, SyntaxRemoveOptions.KeepNoTrivia);

            var documentWithTypeRemoved = documentEditor.GetChangedDocument();
            documentWithTypeRemoved = await documentWithTypeRemoved
                .GetLanguageService<IRemoveUnnecessaryImportsService>()
                .RemoveUnnecessaryImportsAsync(documentWithTypeRemoved, cancellationToken)
                .ConfigureAwait(false);

            var project = documentWithTypeRemoved.Project;
            var namespaceName = typeSymbol.ContainingNamespace.IsGlobalNamespace ? string.Empty : typeSymbol.ContainingNamespace.Name;
            var destinationDocumentName = typeDeclaration.Identifier.ValueText + ".cs";
            var destinationDocumentId = DocumentId.CreateNewId(project.Id, destinationDocumentName);

            if (project.Solution.ContainsDocument(destinationDocumentId))
            {
                // show a UI to get new file name.
            }

            // add using directives
            var compilationUnitSyntax = CompilationUnit()
                    .WithUsings(
                        List(usingStatements.Select(n => (UsingDirectiveSyntax)n)));

            if (!string.IsNullOrEmpty(namespaceName))
            {
                // add namespace declaration and type declaration.
                compilationUnitSyntax = compilationUnitSyntax
                    .WithMembers(
                        SingletonList<MemberDeclarationSyntax>(
                        NamespaceDeclaration(IdentifierName(namespaceName))
                        .WithMembers(
                            SingletonList<MemberDeclarationSyntax>(typeDeclaration))));
            }
            else
            {
                // add type declaration
                compilationUnitSyntax = compilationUnitSyntax
                        .WithMembers(
                            SingletonList<MemberDeclarationSyntax>(typeDeclaration));
            }

            var destinationDocumentText = compilationUnitSyntax
                    .NormalizeWhitespace()
                    .GetText();

            // TODO: make use of folders, fullfilepath.
            // TODO: follow generate type in new file approach of adding an empty document to workspace and then editing its text
            // that way we have the context in which text should be formatted and styles should be applied.
            var newSolution = project.Solution.AddDocument(destinationDocumentId, destinationDocumentName, destinationDocumentText, sourceDocument.Folders);
            var destinationDocument = newSolution.GetDocument(destinationDocumentId);
            destinationDocument = await destinationDocument
                .GetLanguageService<IRemoveUnnecessaryImportsService>()
                .RemoveUnnecessaryImportsAsync(destinationDocument, cancellationToken)
                .ConfigureAwait(false);

            return destinationDocument.Project.Solution;
        }

        //private class MoveTypeCodeAction : CodeAction.SolutionChangeAction
        //{
        //    public MoveTypeCodeAction(string title, Func<CancellationToken, Task<Solution>> createChangedSolution)
        //        : base(title, createChangedSolution, equivalenceKey: title)
        //    {
        //    }

        //    //protected override Task<Solution> GetChangedSolutionAsync(CancellationToken cancellationToken)
        //    //{
        //    //    return base.GetChangedSolutionAsync(cancellationToken);
        //    //}
        //}
    }
}
