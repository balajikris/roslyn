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
    internal abstract partial class AbstractMoveTypeService<TService, TTypeDeclarationSyntax>
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

            public Editor(TService service, SemanticDocument document, bool renameFile, bool moveToNewFile, bool makeTypePartial, bool makeOuterTypePartial, State state, MoveTypeOptionsResult moveTypeOptions, bool fromDialog, CancellationToken cancellationToken)
            {
                _document = document;
                _renameFile = renameFile;
                _moveToNewFile = moveToNewFile;
                _makeTypePartial = makeTypePartial;
                _makeOuterTypePartial = makeOuterTypePartial;
                _renameType = false;
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
                    var newSolution = await Renamer.RenameSymbolAsync(solution, _state.TypeSymbol, _state.TargetFileNameCandidate, _document.Document.Options, _cancellationToken).ConfigureAwait(false);
                    return new CodeActionOperation[] { new ApplyChangesOperation(newSolution) };
                }

                if (_moveToNewFile)
                {
                    var documentName = _fromDialog
                        ? _moveTypeOptions.NewFileName
                        : _state.TargetFileNameCandidate + _state.TargetFileExtension;

                    var namedType = _state.TypeSymbol;
                    var projectToBeUpdated = _document.Project;

                    var documentEditor = await DocumentEditor.CreateAsync(_document.Document, _cancellationToken).ConfigureAwait(false);
                    documentEditor.RemoveNode(_state.TypeNode, SyntaxRemoveOptions.KeepNoTrivia);

                    var documentWithTypeRemoved = documentEditor.GetChangedDocument();
                    documentWithTypeRemoved = await documentWithTypeRemoved
                        .GetLanguageService<IRemoveUnnecessaryImportsService>()
                        .RemoveUnnecessaryImportsAsync(documentWithTypeRemoved, _cancellationToken)
                        .ConfigureAwait(false);

                    projectToBeUpdated = documentWithTypeRemoved.Project;

                    // if documentWithTypeRemoved is empty (without any types left, should we remove the document from solution?

                    var newDocumentId = DocumentId.CreateNewId(projectToBeUpdated.Id, documentName);
                    var newSolution = projectToBeUpdated.Solution.AddDocument(newDocumentId, documentName, string.Empty/*, folders, fullFilePath*/);

                    var newDocument = newSolution.GetDocument(newDocumentId);
                    var newSemanticModel = await newDocument.GetSemanticModelAsync(_cancellationToken).ConfigureAwait(false);
                    var enclosingNamespace = newSemanticModel.GetEnclosingNamespace(0, _cancellationToken);

                    var rootNamespaceOrType = namedType.GenerateRootNamespaceOrType(new string[] { namedType.ContainingNamespace.Name });

                    var codeGenResult = await CodeGenerator.AddNamespaceOrTypeDeclarationAsync(
                        newSolution,
                        enclosingNamespace,
                        rootNamespaceOrType,
                        new CodeGenerationOptions(newSemanticModel.SyntaxTree.GetLocation(new TextSpan())),
                        _cancellationToken).ConfigureAwait(false);

                    var root = await codeGenResult.GetSyntaxRootAsync(_cancellationToken).ConfigureAwait(false);

                    var updatedSolution = newSolution.WithDocumentSyntaxRoot(newDocumentId, root, PreservationMode.PreserveIdentity);
                    updatedSolution = await _service.AddUsingsToDocumentAsync(updatedSolution, root, newDocumentId, _document.Document, _document.Document.Options, _cancellationToken).ConfigureAwait(false);

                    return new CodeActionOperation[] { new ApplyChangesOperation(updatedSolution), new OpenDocumentOperation(newDocumentId) };


                    // add new document to solution
                    // add usings to document
                    // add namespace to document - containing NS of type being moved.
                    // add containing type if necessary, with modifications such as partial.
                    // add type symbol to containing type or namespace.

                }

                return new CodeActionOperation[] { };
            }
        }
    }
}
