// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;

namespace Microsoft.CodeAnalysis.Editor.Implementation.Suggestions
{
    /// <summary>
    /// Base class for light bulb menu items for code fixes and code refactorings.
    /// </summary>
    internal class SuggestedActionWithFlavors : SuggestedAction, ISuggestedActionWithFlavors
    {
        protected SuggestedActionWithFlavors(
            Workspace workspace,
            ITextBuffer subjectBuffer,
            ICodeActionEditHandlerService editHandler,
            CodeAction codeAction,
            object provider) : base(workspace, subjectBuffer, editHandler, codeAction, provider)
        {
        }

        public override bool HasActionSets
        {
            get
            {
                // Light bulb will always invoke this property on the UI thread.
                AssertIsForeground();

                return true;
            }
        }

        private ImmutableArray<SuggestedActionSet> _actionSets;
        public async override Task<IEnumerable<SuggestedActionSet>> GetActionSetsAsync(CancellationToken cancellationToken)
        {
            // Light bulb will invoke this property on the UI thread.
            AssertIsForeground();

            if (_actionSets == null)
            {
                var builder = ImmutableArray.CreateBuilder<SuggestedActionSet>();

                var previewChangesSuggestedActionSet = await GetPreviewChangesSuggestedActionSetAsync(cancellationToken).ConfigureAwait(true);
                if (previewChangesSuggestedActionSet != null)
                {
                    builder.Add(previewChangesSuggestedActionSet);
                }

                var fixAllSuggestedActionSet = GetFixAllSuggestedActionSet();
                if (fixAllSuggestedActionSet != null)
                {
                    builder.Add(fixAllSuggestedActionSet);
                }

                _actionSets = builder.ToImmutable();
            }

            return _actionSets;
        }

        private async Task<SuggestedActionSet> GetPreviewChangesSuggestedActionSetAsync(CancellationToken cancellationToken)
        {
            var previewResult = await GetPreviewResultAsync(cancellationToken).ConfigureAwait(true);
            if (previewResult == null)
            {
                return null;
            }

            var changeSummary = previewResult.ChangeSummary;
            if (changeSummary == null)
            {
                return null;
            }

            var previewAction = new PreviewChangesCodeAction(Workspace, CodeAction, changeSummary);
            var previewSuggestedAction = new PreviewChangesSuggestedAction(Workspace, SubjectBuffer, EditHandler, previewAction, Provider);
            return new SuggestedActionSet(ImmutableArray.Create(previewSuggestedAction));
        }

        protected virtual SuggestedActionSet GetFixAllSuggestedActionSet()
        {
            // Only code fixes support fix all occurrences at the moment. So only
            // CodeFixSuggestedAction provides a non-null-returning implementation for this method.
            return null;
        }
    }
}
