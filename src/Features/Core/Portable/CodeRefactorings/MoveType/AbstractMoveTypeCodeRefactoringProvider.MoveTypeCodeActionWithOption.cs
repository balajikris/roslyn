// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.LanguageServices;
using Microsoft.CodeAnalysis.Notification;
using Microsoft.CodeAnalysis.ProjectManagement;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CodeRefactorings.MoveType
{
    internal abstract partial class AbstractMoveTypeCodeRefactoringProvider<TTypeDeclarationSyntax>
    {
        private class MoveTypeCodeActionWithOption : CodeActionWithOptions
        {
            private readonly SemanticDocument _document;
            private readonly State _state;
            private readonly bool _renameFile;
            private readonly bool _moveToNewFile;
            private readonly bool _makeTypePartial;
            private readonly bool _makeOuterTypePartial;
            private readonly string _title;

            public MoveTypeCodeActionWithOption(
                SemanticDocument document,
                bool renameFile,
                bool moveToNewFile,
                bool makeTypePartial,
                bool makeOuterTypePartial,
                State state)
            {
                _document = document;
                _renameFile = renameFile;
                _moveToNewFile = moveToNewFile;
                _makeTypePartial = makeTypePartial;
                _makeOuterTypePartial = makeOuterTypePartial;
                _state = state;
                _title = CreateDisplayText();
            }

            private string CreateDisplayText()
            {
                if (_moveToNewFile || _makeOuterTypePartial)
                {
                    return $"Move {_state.TypeToMove.Name} via UI";
                }
                else if (_makeTypePartial)
                {
                    return $"Make partial definition for {_state.TypeToMove.Name} via UI";
                }

                return "unexpected path reached - UI";
            }

            public override string Title
            {
                get { return _title; }
            }

            //public override string EquivalenceKey
            //{
            //    get
            //    {
            //        return _state.TargetFileNameCandidate;
            //    }
            //}

            public override object GetOptions(CancellationToken cancellationToken)
            {
                var moveTypeOptionsService = _document.Project.Solution.Workspace.Services.GetService<IMoveTypeOptionsService>();
                var notificationService = _document.Project.Solution.Workspace.Services.GetService<INotificationService>();
                var projectManagementService = _document.Project.Solution.Workspace.Services.GetService<IProjectManagementService>();
                var syntaxFactsService = _document.Project.LanguageServices.GetService<ISyntaxFactsService>();
                var suggestedFileName = _state.TargetFileNameCandidate + _state.TargetFileExtension;

                return moveTypeOptionsService.GetMoveTypeOptions(suggestedFileName, _document.Document, notificationService, projectManagementService, syntaxFactsService);
            }

            protected override async Task<IEnumerable<CodeActionOperation>> ComputeOperationsAsync(object options, CancellationToken cancellationToken)
            {
                IEnumerable<CodeActionOperation> operations = null;

                var moveTypeOptions = options as MoveTypeOptionsResult;
                if (moveTypeOptions != null && !moveTypeOptions.IsCancelled)
                {
                    var editor = new Editor(_document, _renameFile, _moveToNewFile, _makeTypePartial, _makeOuterTypePartial, _state, moveTypeOptions, fromDialog: true, cancellationToken: cancellationToken);
                    operations = await editor.GetOperationsAsync().ConfigureAwait(false);
                }

                return operations;
            }
        }
    }
}
