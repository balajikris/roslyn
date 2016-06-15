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
using Microsoft.CodeAnalysis.ProjectManagement;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Utilities;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CodeRefactorings.MoveType
{
    internal abstract partial class AbstractMoveTypeCodeRefactoringProvider<TTypeDeclarationSyntax>
    {
        private class Editor
        {
            private readonly CancellationToken _cancellationToken;
            private readonly MoveTypeOptionsResult _moveTypeOptions;
            private readonly SemanticDocument _document;
            private readonly State _state;

            private readonly bool _fromDialog;
            private readonly bool _makeOuterTypePartial;
            private readonly bool _makeTypePartial;
            private readonly bool _moveToNewFile;
            private readonly bool _renameFile;
            private readonly bool _renameType;

            public Editor(SemanticDocument document, bool renameFile, bool moveToNewFile, bool makeTypePartial, bool makeOuterTypePartial, State state, MoveTypeOptionsResult moveTypeOptions, bool fromDialog, CancellationToken cancellationToken)
            {
                _document = document;
                _renameFile = renameFile;
                _moveToNewFile = moveToNewFile;
                _makeTypePartial = makeTypePartial;
                _makeOuterTypePartial = makeOuterTypePartial;
                _renameType = false;
                _state = state;
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
                    // TODO: see if this needs .WithId(newID) as well as ID depends on Name.
                    var newDocumentInfo = documentInfo.WithName(name: _state.TargetFileNameCandidate);
                    var newSolution = solution.RemoveDocument(documentInfo.Id);
                    newSolution = newSolution.AddDocument(newDocumentInfo);

                    return new CodeActionOperation[] { new ApplyChangesOperation(newSolution),
                                                       new OpenDocumentOperation(newDocumentInfo.Id) };


                }
                else if (_renameType)
                {
                    // TODO: there is no codeaction exposed for this codepath.
                    var newSolution = await Renamer.RenameSymbolAsync(solution, _state.TypeToMove, _state.TargetFileNameCandidate, _document.Document.Options, _cancellationToken).ConfigureAwait(false);
                    return new CodeActionOperation[] { new ApplyChangesOperation(newSolution) };
                }

                return new CodeActionOperation[] { };
            }
        }
    }
}
