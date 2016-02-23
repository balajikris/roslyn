using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.CodeStyle.TypingStyle
{
    internal abstract partial class AbstractTypingStyleService<TService> : ITypingStyleService
        where TService : AbstractTypingStyleService<TService>
    {
        protected abstract bool IsInVariableDeclarationContext(SyntaxNode declaration);
        protected abstract bool ShouldAnalyzeVariableDeclaration(SyntaxNode declaration, SemanticModel semanticModel, CancellationToken cancellationToken);
        protected abstract bool TryAnalyzeVariableDeclaration(SyntaxNode declaration, SemanticModel semanticModel, OptionSet optionSet, CancellationToken cancellationToken, out TextSpan issueSpan);

        public bool ShouldUseImplicitTyping(SyntaxNode initializer, SemanticModel semanticModel, OptionSet optionSet, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public bool ShouldSimplifyTypingInDeclaration(SyntaxNode declaration, SemanticModel semanticModel, OptionSet optionSet, CancellationToken cancellationToken)
        {
            var isVariableDeclaration = IsInVariableDeclarationContext(declaration);
            var shouldAnalyze = true;

            if (isVariableDeclaration)
            {
                shouldAnalyze = ShouldAnalyzeVariableDeclaration(declaration, semanticModel, cancellationToken);
            }

            if (shouldAnalyze)
            {
                var state = State.Generate((TService)this, declaration, semanticModel, optionSet, isVariableDeclaration, cancellationToken);

                if (IsImplicitTypingPreferred(semanticModel, optionSet, state, cancellationToken))
                {
                    TextSpan span;
                    return TryAnalyzeVariableDeclaration(declaration, semanticModel, optionSet, cancellationToken, out span);
                }
            }

            return false;
        }

        private bool IsImplicitTypingPreferred(SemanticModel semanticModel, OptionSet optionSet, State state, CancellationToken cancellationToken)
        {
            var stylePreferences = state.StylePreferences;
            var shouldNotify = state.ShouldNotify();

            // If notification preference is None, don't offer the suggestion.
            if (!shouldNotify)
            {
                return false;
            }

            if (state.IsInIntrinsicTypeContext)
            {
                return stylePreferences.HasFlag(TypingStyles.VarForIntrinsic);
            }
            else if (state.IsTypingApparentInContext)
            {
                return stylePreferences.HasFlag(TypingStyles.VarWhereApparent);
            }
            else
            {
                return stylePreferences.HasFlag(TypingStyles.VarWherePossible);
            }
        }
    }
}
