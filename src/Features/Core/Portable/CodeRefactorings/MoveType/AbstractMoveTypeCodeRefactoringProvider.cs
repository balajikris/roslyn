using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.CodeRefactorings.MoveType
{
    internal abstract partial class AbstractMoveTypeCodeRefactoringProvider<TTypeDeclarationSyntax> : CodeRefactoringProvider
        where TTypeDeclarationSyntax : SyntaxNode
    {
        public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            var document = context.Document;
            var textSpan = context.Span;
            var cancellationToken = context.CancellationToken;

            if (!textSpan.IsEmpty)
            {
                return;
            }

            if (document.Project.Solution.Workspace.Kind == WorkspaceKind.MiscellaneousFiles)
            {
                return;
            }

            var refactoring = await GetRefactoringAsync(document, textSpan, cancellationToken).ConfigureAwait(false);
            if (refactoring != null)
            {
                context.RegisterRefactorings(refactoring.Actions);
            }
        }

        protected async Task<CodeRefactoring> GetRefactoringAsync(Document document, TextSpan textSpan, CancellationToken cancellationToken)
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
                actions.AddRange(
                    GetCodeActions(
                        document, state, mustShowDialog,
                        renameFile: false, moveToNewFile: true, makeTypePartial: false, makeOuterTypePartial: true));
            }
            else
            {
                if (state.OnlyTypeInFile)
                {
                    //Todo: clean up this, showDialog means can't move or rename etc.. feels weird.
                    // rename file.
                    actions.AddRange(
                        GetCodeActions(
                            document, state, mustShowDialog,
                            renameFile: true, moveToNewFile: false, makeTypePartial: false, makeOuterTypePartial: false));

                    // make partial and create a partial decl in a new file
                    actions.AddRange(
                        GetCodeActions(
                            document, state, mustShowDialog: true,
                            renameFile: false, moveToNewFile: false, makeTypePartial: true, makeOuterTypePartial: false));
                }
                else
                {
                    // straight forward case, not the only type in this file, move type to a new file.
                    actions.Add(
                        GetSimpleCodeAction(
                            document, renameFile: false, moveToNewFile: true, makeTypePartial: false, makeOuterTypePartial: false, state: state));
                }
            }

            return actions;
        }

        private static IEnumerable<CodeAction> GetCodeActions(
            SemanticDocument document,
            State state,
            bool mustShowDialog,
            bool renameFile,
            bool moveToNewFile,
            bool makeTypePartial,
            bool makeOuterTypePartial)
        {
            var codeActionWithOption = (CodeAction) new MoveTypeCodeActionWithOption(
                document, renameFile, moveToNewFile, makeTypePartial, makeOuterTypePartial, state);

            var actions = new List<CodeAction> { codeActionWithOption };

            if (!mustShowDialog)
            {
                actions.Add(GetSimpleCodeAction(
                    document, renameFile, moveToNewFile, makeTypePartial, makeOuterTypePartial, state));
            }

            return actions;
        }

        private static MoveTypeCodeAction GetSimpleCodeAction(SemanticDocument document, bool renameFile, bool moveToNewFile, bool makeTypePartial, bool makeOuterTypePartial, State state)
        {
            return new MoveTypeCodeAction(document, renameFile, moveToNewFile, makeTypePartial, makeOuterTypePartial, state);
        }
    }
}
