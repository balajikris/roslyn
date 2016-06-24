// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeGeneration;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.ProjectManagement;
using Microsoft.CodeAnalysis.RemoveUnnecessaryImports;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Utilities;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CodeRefactorings.MoveType
{
    internal abstract partial class AbstractMoveTypeService<TService, TTypeDeclarationSyntax, TNamespaceDeclarationSyntax, TMemberDeclarationSyntax>
    {
        private class Editor
        {
            private readonly CancellationToken _cancellationToken;
            private readonly MoveTypeOptionsResult _moveTypeOptions;
            private readonly SemanticDocument _document;
            private readonly State _state;
            private readonly TService _service;

            private readonly bool _fromDialog;
            private readonly bool _makeOuterTypePartial;
            private readonly bool _makeTypePartial;
            private readonly bool _moveToNewFile;
            private readonly bool _renameFile;
            private readonly bool _renameType;

            public Editor(
                TService service,
                SemanticDocument document,
                bool renameFile,
                bool renameType,
                bool moveToNewFile,
                bool makeTypePartial,
                bool makeOuterTypePartial,
                State state,
                MoveTypeOptionsResult moveTypeOptions,
                bool fromDialog,
                CancellationToken cancellationToken)
            {
                _document = document;
                _renameFile = renameFile;
                _moveToNewFile = moveToNewFile;
                _makeTypePartial = makeTypePartial;
                _makeOuterTypePartial = makeOuterTypePartial;
                _renameType = renameType;
                _state = state;
                _service = service;
                this._moveTypeOptions = moveTypeOptions;
                this._fromDialog = fromDialog;
                this._cancellationToken = cancellationToken;
            }

            internal async Task<IEnumerable<CodeActionOperation>> GetOperationsAsync()
            {
                var solution = _document.Document.Project.Solution;
                var documentInfo = _document.Document.GetDocumentState().Info;

                if (_renameFile)
                {
                    var text = await _document.Document.GetTextAsync(_cancellationToken).ConfigureAwait(false);
                    var newDocumentId = DocumentId.CreateNewId(_document.Document.Project.Id, _state.TargetFileNameCandidate);

                    var newSolution = solution.RemoveDocument(documentInfo.Id);
                    newSolution = newSolution.AddDocument(newDocumentId, _state.TargetFileNameCandidate, text);

                    return new CodeActionOperation[] { new ApplyChangesOperation(newSolution),
                                                       new OpenDocumentOperation(newDocumentId) };
                }
                else if (_renameType)
                {
                    // TODO: there is no codeaction exposed for this codepath.
                    var newSolution = await Renamer.RenameSymbolAsync(solution, _state.TypeSymbol, _state.DocumentName, _document.Document.Options, _cancellationToken).ConfigureAwait(false);
                    return new CodeActionOperation[] { new ApplyChangesOperation(newSolution) };
                }

                if (_moveToNewFile)
                {
                    var documentName = _fromDialog
                        ? _moveTypeOptions.NewFileName
                        : _state.TargetFileNameCandidate + _state.TargetFileExtension;

                    // fork source document, keep required type/namespace hierarchy and add it to a new document
                    var projectToBeUpdated = _document.Document.Project;
                    var newDocumentId = DocumentId.CreateNewId(projectToBeUpdated.Id, documentName);

                    var solutionWithNewDocument = await AddNewDocumentWithTypeDeclarationAsync(
                        _document, documentName, newDocumentId, _state.TypeNode, _cancellationToken).ConfigureAwait(false);

                    // Get the original source document again, from the latest forked solution.
                    var sourceDocument = solutionWithNewDocument.GetDocument(_document.Document.Id);

                    // remove type declaration from original source document and perform clean up operations like remove unused usings.
                    var solutionWithBothDocumentsUpdated = await RemoveTypeDeclarationFromSourceDocumentAsync(
                        sourceDocument, _state.TypeNode, _cancellationToken).ConfigureAwait(false);

                    return new CodeActionOperation[] { new ApplyChangesOperation(solutionWithBothDocumentsUpdated), new OpenDocumentOperation(newDocumentId) };
                }

                return new CodeActionOperation[] { };
            }

            private async Task<Solution> AddNewDocumentWithTypeDeclarationAsync(
                SemanticDocument sourceDocument,
                string newDocumentName,
                DocumentId newDocumentId,
                TTypeDeclarationSyntax typeNode,
                CancellationToken cancellationToken)
            {
                var root = sourceDocument.Root;
                var projectToBeUpdated = sourceDocument.Document.Project;

                // remove all types and namespaces not in the ancestor chain of the one we've moving.
                var ancestorNamespaces = typeNode.Ancestors().OfType<TNamespaceDeclarationSyntax>();
                var ancestorTypesAndSelf = typeNode.AncestorsAndSelf().OfType<TTypeDeclarationSyntax>();
                var descendentMembers = typeNode
                    .DescendantNodes(descendIntoChildren: _ => true, descendIntoTrivia: false)
                    .OfType<TMemberDeclarationSyntax>();

                var membersToRemove = root
                    .DescendantNodesAndSelf(descendIntoChildren: _ => true, descendIntoTrivia: false)
                    .OfType<TMemberDeclarationSyntax>()
                    .Where(n => UnaffiliatedMembers(n, ancestorNamespaces, ancestorTypesAndSelf, descendentMembers));

                root = root.RemoveNodes(membersToRemove, SyntaxRemoveOptions.KeepNoTrivia);

                // TODO: adjust partial modifier

                // add an empty document to solution
                var solutionWithNewDocument = projectToBeUpdated.Solution.AddDocument(newDocumentId, newDocumentName, string.Empty/*, folders, fullFilePath*/);

                // update the text for the new document
                solutionWithNewDocument = solutionWithNewDocument.WithDocumentSyntaxRoot(newDocumentId, root, PreservationMode.PreserveIdentity);

                // get the updated document, perform clean up like remove unused usings.
                var newDocument = solutionWithNewDocument.GetDocument(newDocumentId);
                return await CleanUpDocumentAsync(newDocument, cancellationToken).ConfigureAwait(false);
            }

            private bool UnaffiliatedMembers(
                SyntaxNode node,
                IEnumerable<TNamespaceDeclarationSyntax> ancestorNamespaces,
                IEnumerable<TTypeDeclarationSyntax> ancestorTypesAndSelf,
                IEnumerable<TMemberDeclarationSyntax> descendentMembers)
            {
                // 1. Keep namespaces that are in ancestor chain of type being moved.
                // 2. Keep type being moved and any type declaration in its ancestor chain.
                //    2a.  For types in ancestor chain being kept, keep no members, except just the type being moved.
                //    2b.  For type being moved, keep all descendent members as such.
                return node is TNamespaceDeclarationSyntax
                    ? !ancestorNamespaces.Contains(node)
                    : node is TTypeDeclarationSyntax
                        ? !ancestorTypesAndSelf.Contains(node)
                        : !descendentMembers.Contains(node);
            }

            private async Task<Solution> RemoveTypeDeclarationFromSourceDocumentAsync(
                Document sourceDocument, TTypeDeclarationSyntax typeNode, CancellationToken cancellationToken)
            {
                var documentEditor = await DocumentEditor.CreateAsync(sourceDocument, cancellationToken).ConfigureAwait(false);
                documentEditor.RemoveNode(typeNode, SyntaxRemoveOptions.KeepNoTrivia);
                var updatedDocument = documentEditor.GetChangedDocument();

                // TODO: make this optional -- do not remove other parts of code from source document? 
                return await CleanUpDocumentAsync(updatedDocument, cancellationToken).ConfigureAwait(false);
            }

            private async Task<Solution> CleanUpDocumentAsync(Document document, CancellationToken cancellationToken)
            {
                document = await document
                    .GetLanguageService<IRemoveUnnecessaryImportsService>()
                    .RemoveUnnecessaryImportsAsync(document, cancellationToken)
                    .ConfigureAwait(false);

                var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
                return document.Project.Solution.WithDocumentSyntaxRoot(document.Id, root, PreservationMode.PreserveIdentity);

                //TODO: if documentWithTypeRemoved is empty (without any types left, should we remove the document from solution?
            }
        }
    }
}
