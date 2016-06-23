using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.CodeRefactorings.MoveType
{
    internal abstract partial class AbstractMoveTypeService<TService, TTypeDeclarationSyntax> : IMoveTypeService
        where TService : AbstractMoveTypeService<TService, TTypeDeclarationSyntax>
        where TTypeDeclarationSyntax : SyntaxNode
    {
        protected abstract Task<Solution> AddUsingsToDocumentAsync(Solution updatedSolution, SyntaxNode modifiedRoot, DocumentId targetDocumentId, Document sourceDocument, DocumentOptionSet options, CancellationToken cancellationToken);

        public async Task<CodeRefactoring> GetRefactoringAsync(Document document, TextSpan textSpan, CancellationToken cancellationToken)
        {
            var semanticDocument = await SemanticDocument.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            var state = State.Generate(semanticDocument, textSpan, cancellationToken);
            if (state == null)
            {
                return null;
            }

            var actions = CreateActions(semanticDocument, state, cancellationToken);
            if (actions.Count == 0)
            {
                return null;
            }

            return new CodeRefactoring(null, actions);
        }

        private List<CodeAction> CreateActions(SemanticDocument document, State state, CancellationToken cancellationToken)
        {
            var actions = new List<CodeAction>();
            var mustShowDialog = state.TargetFileNameAlreadyExists;

            if (state.IsNestedType)
            {
                // nested type, make outer type partial and move type into a new file inside a partial part.
                actions.Add(GetSimpleCodeAction(
                    document, state, renameFile: false, moveToNewFile: true, makeTypePartial: false, makeOuterTypePartial: true));
                actions.Add(GetCodeActionWithUI(
                    document, state, renameFile: false, moveToNewFile: true, makeTypePartial: false, makeOuterTypePartial: true));
            }
            else
            {
                if (state.OnlyTypeInFile)
                {
                    //Todo: clean up this, showDialog means can't move or rename etc.. feels weird.
                    // rename file.
                    actions.Add(GetSimpleCodeAction(
                        document, state, renameFile: true, moveToNewFile: false, makeTypePartial: false, makeOuterTypePartial: false));

                    // make partial and create a partial decl in a new file
                    actions.Add(GetSimpleCodeAction(
                        document, state, renameFile: false, moveToNewFile: false, makeTypePartial: true, makeOuterTypePartial: false));

                    actions.Add(GetCodeActionWithUI(
                        document, state, renameFile: false, moveToNewFile: false, makeTypePartial: true, makeOuterTypePartial: false));
                }
                else
                {
                    // straight forward case, not the only type in this file, move type to a new file.
                    actions.Add(GetSimpleCodeAction(
                        document, renameFile: false, moveToNewFile: true, makeTypePartial: false, makeOuterTypePartial: false, state: state));
                    actions.Add(GetCodeActionWithUI(
                        document, renameFile: false, moveToNewFile: true, makeTypePartial: false, makeOuterTypePartial: false, state: state));
                }
            }

            return actions;
        }

        private CodeAction GetCodeActionWithUI(SemanticDocument document, State state, bool renameFile, bool moveToNewFile, bool makeTypePartial, bool makeOuterTypePartial)
        {
            return new MoveTypeCodeActionWithOption((TService)this, document, renameFile, moveToNewFile, makeTypePartial, makeOuterTypePartial, state);
        }

        private MoveTypeCodeAction GetSimpleCodeAction(SemanticDocument document, State state, bool renameFile, bool moveToNewFile, bool makeTypePartial, bool makeOuterTypePartial)
        {
            return new MoveTypeCodeAction((TService)this, document, renameFile, moveToNewFile, makeTypePartial, makeOuterTypePartial, state);
        }
    }
}
