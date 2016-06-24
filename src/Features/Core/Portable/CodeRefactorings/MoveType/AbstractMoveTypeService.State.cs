using System;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis.LanguageServices;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.CodeRefactorings.MoveType
{
    internal abstract partial class AbstractMoveTypeService<TService, TTypeDeclarationSyntax, TNamespaceDeclarationSyntax, TMemberDeclarationSyntax>
    {
        protected class State
        {
            public SemanticDocument Document { get; }
            public string DocumentName { get; set; }

            // list of properties that capture state
            public bool IsNestedType { get; private set; }
            // The user may yet want to make this type partial and create a partial def in a new file
            public bool TypeNameMatchesFileName { get; set; }
            public bool MakeTypePartial { get; set; }
            public bool MakeContainingTypePartial { get; set; }
            public bool PromoteType { get; set; }
            // Design: should we move type to new file, or just rename curent file -- that would be a codestyle - match file with typename
            // suppose we move type to new file, what should we do with old file -- leave it or delete?
            public bool OnlyTypeInFile { get; set; }
            // say type name does not match file name but a file with destination name 
            // already exists in the project.
            // Design: Should do CodeActionWithOption to get a different filename from user or move this type into an existing file.
            public bool TargetFileNameAlreadyExists { get; set; }

            public string TargetFileNameCandidate { get; set; }
            public string TargetFileExtension { get; set; }

            //public INamespaceSymbol TargetNamespace { get; set; }
            //public INamedTypeSymbol TargetContainingType { get; set; }
            public INamedTypeSymbol TypeSymbol { get; set; }
            public TTypeDeclarationSyntax TypeNode { get; set;}

            private State(SemanticDocument document)
            {
                this.Document = document;
            }

            internal static State Generate(SemanticDocument document, TextSpan textSpan, CancellationToken cancellationToken)
            {
                var state = new State(document);
                if (!state.TryInitialize(textSpan, cancellationToken))
                {
                    return null;
                }

                return state;
            }

            private bool TryInitialize(
                TextSpan textSpan,
                CancellationToken cancellationToken)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return false;
                }

                // determine state and set properties.
                var tree = this.Document.SyntaxTree;
                var root = this.Document.Root;
                var syntaxFacts = this.Document.Project.LanguageServices.GetService<ISyntaxFactsService>();

                var typeDeclaration = root.FindNode(textSpan) as TTypeDeclarationSyntax;
                if (typeDeclaration == null)
                {
                    return false;
                }

                var typeSymbol = this.Document.SemanticModel.GetDeclaredSymbol(typeDeclaration, cancellationToken) as INamedTypeSymbol;

                // compiler declared types, anonymous types, types defined in metadata should be filtered out.
                if (typeSymbol == null ||
                    typeSymbol.Locations.Any(loc => loc.IsInMetadata) ||
                    typeSymbol.IsAnonymousType ||
                    typeSymbol.IsImplicitlyDeclared)
                {
                    return false;
                }

                TypeNode = typeDeclaration;
                TypeSymbol = typeSymbol;

                IsNestedType = typeDeclaration.Parent is TTypeDeclarationSyntax;
                OnlyTypeInFile = this.Document.Root.DescendantNodes().OfType<TTypeDeclarationSyntax>().Count() == 1;

                DocumentName = Path.GetFileNameWithoutExtension(this.Document.Document.Name);
                TypeNameMatchesFileName = string.Equals(DocumentName, typeSymbol.Name, StringComparison.CurrentCultureIgnoreCase);
                TargetFileNameCandidate = typeSymbol.Name;
                TargetFileExtension = this.Document.Document.Project.Language == LanguageNames.CSharp ? ".cs" : ".vb";

                if (!TypeNameMatchesFileName)
                {
                    var destinationDocumentId = DocumentId.CreateNewId(this.Document.Project.Id, TargetFileNameCandidate + TargetFileExtension);
                    TargetFileNameAlreadyExists = this.Document.Project.ContainsDocument(destinationDocumentId);
                }
                else
                {
                    TargetFileNameAlreadyExists = true;
                }

                return true;
            }
        }
    }
}