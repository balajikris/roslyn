using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeActions;

namespace Microsoft.CodeAnalysis.CodeRefactorings.MoveType
{
    internal abstract partial class AbstractMoveTypeService<TService, TTypeDeclarationSyntax, TNamespaceDeclarationSyntax, TMemberDeclarationSyntax>
    {
        private class MoveTypeCodeAction : CodeAction
        {
            private readonly SemanticDocument _document;
            private readonly State _state;
            private readonly TService _service;

            private readonly bool _renameFile;
            private readonly bool _moveToNewFile;
            private readonly bool _makeTypePartial;
            private readonly bool _makeOuterTypePartial;
            private readonly string _title;
            private readonly bool _renameType;

            public MoveTypeCodeAction(
                TService service,
                SemanticDocument document,
                bool renameFile,
                bool renameType,
                bool moveToNewFile,
                bool makeTypePartial,
                bool makeOuterTypePartial,
                State state)
            {
                _document = document;
                _renameFile = renameFile;
                _renameType = renameType;
                _moveToNewFile = moveToNewFile;
                _makeTypePartial = makeTypePartial;
                _makeOuterTypePartial = makeOuterTypePartial;
                _state = state;
                _service = service;
                _title = CreateDisplayText();
            }

            private string CreateDisplayText()
            {
                if (_renameFile)
                {
                    return $"Rename File to {_state.TargetFileNameCandidate + _state.TargetFileExtension}";
                }
                else if (_renameType)
                {
                    return $"Rename Type '{_state.TypeSymbol.Name}' to match file name";
                }
                else if (_moveToNewFile || _makeOuterTypePartial)
                {
                    return $"Move Type to {_state.TargetFileNameCandidate + _state.TargetFileExtension}";
                }
                else if (_makeTypePartial)
                {
                    return $"Make partial definition for {_state.TypeSymbol.Name}";
                }

                return "unexpected path reached";
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

            protected override async Task<IEnumerable<CodeActionOperation>> ComputeOperationsAsync(CancellationToken cancellationToken)
            {
                // var moveTypeOptions = new MoveTypeOptionsResult(_state.TargetFileNameCandidate);
                // TODO: Make another constructor overload that doesn't require MoveTypeOptions.
                var editor = new Editor(_service, _document, _renameFile, _renameType, _moveToNewFile, _makeTypePartial, _makeOuterTypePartial, _state, moveTypeOptions: null, fromDialog: false, cancellationToken: cancellationToken);
                return await editor.GetOperationsAsync().ConfigureAwait(false);
            }
        }

    }
}
