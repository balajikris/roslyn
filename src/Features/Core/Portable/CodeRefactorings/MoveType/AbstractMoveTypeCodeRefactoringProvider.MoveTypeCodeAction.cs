using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeActions;

namespace Microsoft.CodeAnalysis.CodeRefactorings.MoveType
{
    internal abstract partial class AbstractMoveTypeCodeRefactoringProvider<TTypeDeclarationSyntax>
    {
        private class MoveTypeCodeAction : CodeAction
        {
            private readonly SemanticDocument _document;
            private readonly State _state;
            private readonly bool _renameFile;
            private readonly bool _moveToNewFile;
            private readonly bool _makeTypePartial;
            private readonly bool _makeOuterTypePartial;

            public MoveTypeCodeAction(
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
            }

            public override string Title
            {
                get
                {
                    return "Move Type to File...";
                }
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
                var editor = new Editor(_document, _renameFile, _moveToNewFile, _makeTypePartial, _makeOuterTypePartial, _state, moveTypeOptions: null, fromDialog: false, cancellationToken: cancellationToken);
                return await editor.GetOperationsAsync().ConfigureAwait(false);
            }
        }

    }
}
