using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Options;

namespace Microsoft.CodeAnalysis.CodeStyle.TypingStyle
{
    internal interface ITypingStyleService : ILanguageService
    {
        //bool ShouldUseImplicitTyping(SyntaxNode declaration, SemanticModel semanticModel, OptionSet optionSet, CancellationToken cancellationToken);

        bool ShouldUseImplicitTyping(SyntaxNode initializer, SemanticModel semanticModel, OptionSet optionSet, CancellationToken cancellationToken);
        bool ShouldSimplifyTypingInDeclaration(SyntaxNode declaration, SemanticModel semanticModel, OptionSet optionSet, CancellationToken cancellationToken);

        //bool ShouldUseExplicitTyping(TDeclarationSyntax declaration, SemanticModel semanticModel, OptionSet optionSet, CancellationToken cancellationToken);
        //bool ShouldUseExplicitTyping(TExpressionSyntax initializer, SemanticModel semanticModel, OptionSet optionSet, CancellationToken cancellationToken);
    }
}
