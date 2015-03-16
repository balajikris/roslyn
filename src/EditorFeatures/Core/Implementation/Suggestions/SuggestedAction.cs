// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.Editor.Host;
using Microsoft.CodeAnalysis.Editor.Shared.Utilities;
using Microsoft.CodeAnalysis.Extensions;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Editor.Implementation.Suggestions
{
    /// <summary>
    /// Base class for all Roslyn light bulb menu items.
    /// </summary>
    internal partial class SuggestedAction : ForegroundThreadAffinitizedObject, ISuggestedAction, IEquatable<ISuggestedAction>
    {
        protected readonly Workspace Workspace;
        protected readonly ITextBuffer SubjectBuffer;
        protected readonly ICodeActionEditHandlerService EditHandler;

        protected readonly object Provider;
        protected readonly CodeAction CodeAction;

        protected SuggestedAction(
            Workspace workspace,
            ITextBuffer subjectBuffer,
            ICodeActionEditHandlerService editHandler,
            CodeAction codeAction,
            object provider)
        {
            Contract.ThrowIfTrue(provider == null);

            this.Workspace = workspace;
            this.SubjectBuffer = subjectBuffer;
            this.CodeAction = codeAction;
            this.EditHandler = editHandler;
            this.Provider = provider;
        }

        public bool TryGetTelemetryId(out Guid telemetryId)
        {
            // TODO: this is temporary. Diagnostic team needs to figure out how to provide unique id per a fix.
            // for now, we will use type of CodeAction, but there are some predefined code actions that are used by multiple fixes
            // and this will not distinguish those

            // AssemblyQualifiedName will change across version numbers, FullName won't
            var type = CodeAction.GetType();
            type = type.IsConstructedGenericType ? type.GetGenericTypeDefinition() : type;

            telemetryId = new Guid(type.FullName.GetHashCode(), 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
            return true;
        }

        public virtual void Invoke(CancellationToken cancellationToken)
        {
            var snapshot = this.SubjectBuffer.CurrentSnapshot;

            using (new CaretPositionRestorer(this.SubjectBuffer, this.EditHandler.AssociatedViewService))
            {
                var extensionManager = this.Workspace.Services.GetService<IExtensionManager>();
                extensionManager.PerformAction(Provider, () =>
                {
                    IEnumerable<CodeActionOperation> operations = null;

                    // NOTE: We want to avoid computing the operations on the UI thread, so we will kick off a task to GetOperations.
                    // However, for CodeActionWithOptions, GetOptions might involve spinning up a dialog to compute the options and must be done on the UI thread.
                    // Hence we need the below if-else statement instead of just invoking CodeAction.GetOperationsAsync()
                    var actionWithOptions = this.CodeAction as CodeActionWithOptions;
                    if (actionWithOptions != null)
                    {
                        var options = actionWithOptions.GetOptions(cancellationToken);
                        if (options != null)
                        {
                            operations = Task.Run(
                                async () => await actionWithOptions.GetOperationsAsync(options, cancellationToken).ConfigureAwait(false), cancellationToken).WaitAndGetResult(cancellationToken);
                        }
                    }
                    else
                    {
                        operations = Task.Run(
                            async () => await this.CodeAction.GetOperationsAsync(cancellationToken).ConfigureAwait(false), cancellationToken).WaitAndGetResult(cancellationToken);
                    }

                    if (operations != null)
                    {
                        var document = this.SubjectBuffer.CurrentSnapshot.GetOpenDocumentInCurrentContextWithChanges();
                        EditHandler.Apply(Workspace, document, operations, CodeAction.Title, cancellationToken);
                    }
                });
            }
        }

        public string DisplayText
        {
            get
            {
                // Underscores will become an accelerator in the VS smart tag.  So we double all
                // underscores so they actually get represented as an underscore in the UI.
                var extensionManager = this.Workspace.Services.GetService<IExtensionManager>();
                var text = extensionManager.PerformFunction(Provider, () => CodeAction.Title, string.Empty);
                return text.Replace("_", "__");
            }
        }

        private IEnumerable<CodeActionOperation> _operations;
        protected async Task<SolutionPreviewResult> GetPreviewResultAsync(CancellationToken cancellationToken)
        {
            AssertIsForeground();

            if (_operations == null)
            {
                // We use ConfigureAwait(true) to stay on the UI thread.
                _operations = await this.CodeAction.GetPreviewOperationsAsync(cancellationToken).ConfigureAwait(true);
            }

            return EditHandler.GetPreviews(Workspace, _operations, cancellationToken);
        }

        public virtual bool HasPreview
        {
            get
            {
                // Light bulb will always invoke this property on the UI thread.
                AssertIsForeground();

                return true;
            }
        }

        public virtual async Task<object> GetPreviewAsync(CancellationToken cancellationToken)
        {
            // Light bulb will always invoke this function on the UI thread.
            AssertIsForeground();

            var extensionManager = this.Workspace.Services.GetService<IExtensionManager>();
            var previewContent = await extensionManager.PerformFunctionAsync(Provider, async () =>
            {
                // We need to stay on UI thread after GetPreviewResultAsync() so that TakeNextPreviewAsync()
                // below can execute on UI thread. We use ConfigureAwait(true) to stay on the UI thread.
                var previewResult = await GetPreviewResultAsync(cancellationToken).ConfigureAwait(true);
                if (previewResult == null)
                {
                    return null;
                }
                else
                {
                    var preferredDocumentId = Workspace.GetDocumentIdInCurrentContext(SubjectBuffer.AsTextContainer());
                    var preferredProjectid = preferredDocumentId == null ? null : preferredDocumentId.ProjectId;

                    // TakeNextPreviewAsync() needs to run on UI thread.
                    AssertIsForeground();
                    return await previewResult.TakeNextPreviewAsync(preferredDocumentId, preferredProjectid, cancellationToken).ConfigureAwait(true);
                }

                // GetPreviewPane() below needs to run on UI thread. We use ConfigureAwait(true) to stay on the UI thread.
            }).ConfigureAwait(true);

            var previewPaneService = Workspace.Services.GetService<IPreviewPaneService>();
            if (previewPaneService == null)
            {
                return null;
            }

            // GetPreviewPane() needs to run on the UI thread.
            AssertIsForeground();
            return previewPaneService.GetPreviewPane(GetDiagnostic(), previewContent);
        }

        protected virtual Diagnostic GetDiagnostic()
        {
            return null;
        }

        #region not supported
        void IDisposable.Dispose()
        {
            // do nothing
        }

        public virtual bool HasActionSets
        {
            get
            {
                return false;
            }
        }

        public virtual Task<IEnumerable<SuggestedActionSet>> GetActionSetsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IEnumerable<SuggestedActionSet>>(null);
        }

        string ISuggestedAction.IconAutomationText
        {
            get
            {
                // same as display text
                return DisplayText;
            }
        }

        ImageMoniker ISuggestedAction.IconMoniker
        {
            get
            {
                // no icon support
                return default(ImageMoniker);
            }
        }

        string ISuggestedAction.InputGestureText
        {
            get
            {
                // no shortcut support
                return null;
            }
        }
        #endregion

        #region IEquatable<ISuggestedAction>
        public bool Equals(ISuggestedAction other)
        {
            return Equals(other as SuggestedAction);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as SuggestedAction);
        }

        public bool Equals(SuggestedAction otherSuggestedAction)
        {
            if (otherSuggestedAction == null)
            {
                return false;
            }

            if (ReferenceEquals(this, otherSuggestedAction))
            {
                return true;
            }

            if (!ReferenceEquals(Provider, otherSuggestedAction.Provider))
            {
                return false;
            }

            var otherCodeAction = otherSuggestedAction.CodeAction;
            if (CodeAction.EquivalenceKey == null || otherCodeAction.EquivalenceKey == null)
            {
                return false;
            }

            return CodeAction.EquivalenceKey == otherCodeAction.EquivalenceKey;
        }

        public override int GetHashCode()
        {
            if (CodeAction.EquivalenceKey == null)
            {
                return base.GetHashCode();
            }

            return Hash.Combine(Provider.GetHashCode(), CodeAction.EquivalenceKey.GetHashCode());
        }
        #endregion
    }
}
