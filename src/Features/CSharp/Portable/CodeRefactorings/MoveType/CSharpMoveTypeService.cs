using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeRefactorings.MoveType;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Shared.Options;
using Microsoft.CodeAnalysis.Formatting;

namespace Microsoft.CodeAnalysis.CSharp.CodeRefactorings.MoveType
{
    [ExportLanguageService(typeof(IMoveTypeService), LanguageNames.CSharp), Shared]
    internal class CSharpMoveTypeService : AbstractMoveTypeService<CSharpMoveTypeService, BaseTypeDeclarationSyntax>
    {
        protected override async Task<Solution> AddUsingsToDocumentAsync(Solution updatedSolution, SyntaxNode modifiedRoot, DocumentId targetDocumentId, Document sourceDocument, DocumentOptionSet options, CancellationToken cancellationToken)
        {
            if (modifiedRoot is CompilationUnitSyntax)
            {
                var compilationRoot = (CompilationUnitSyntax)modifiedRoot;

                var originalRoot = await sourceDocument.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
                var usingDirectives = originalRoot.DescendantNodesAndSelf().OfType<UsingDirectiveSyntax>();

                // Nothing to include
                if (!usingDirectives.Any())
                {
                    return updatedSolution;
                }

                var placeSystemNamespaceFirst = options.GetOption(OrganizerOptions.PlaceSystemNamespaceFirst);
                var addedCompilationRoot = compilationRoot.AddUsingDirectives(usingDirectives.ToList(), placeSystemNamespaceFirst, new SyntaxAnnotation[] { Formatter.Annotation });
                updatedSolution = updatedSolution.WithDocumentSyntaxRoot(targetDocumentId, addedCompilationRoot, PreservationMode.PreserveIdentity);
            }

            return updatedSolution;
        }
    }
}
