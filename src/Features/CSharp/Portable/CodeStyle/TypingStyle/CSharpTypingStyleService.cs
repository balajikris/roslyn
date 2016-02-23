// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeStyle;
using Microsoft.CodeAnalysis.CodeStyle.TypingStyle;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp.CodeStyle.TypingStyle
{
    [ExportLanguageService(typeof(ITypingStyleService), LanguageNames.CSharp), Shared]
    internal class CSharpTypingStyleService :
        AbstractTypingStyleService<CSharpTypingStyleService>
    {
        protected override bool IsInVariableDeclarationContext(SyntaxNode declaration) => 
            declaration.IsKind(SyntaxKind.VariableDeclaration);

        protected override bool ShouldAnalyzeVariableDeclaration(SyntaxNode declaration, SemanticModel semanticModel, CancellationToken cancellationToken)
        {
            var variableDeclaration = declaration as VariableDeclarationSyntax;
            Contract.Requires(variableDeclaration != null, "given declaration is not a valid Variable Declaration");

            // var is applicable only for local variables.
            if (variableDeclaration.Parent.IsKind(SyntaxKind.FieldDeclaration) ||
                variableDeclaration.Parent.IsKind(SyntaxKind.EventFieldDeclaration))
            {
                return false;
            }

            // implicitly typed variables cannot have multiple declarators and
            // must have an initializer.
            if (variableDeclaration.Variables.Count > 1 ||
                !variableDeclaration.Variables.Single().Initializer.IsKind(SyntaxKind.EqualsValueClause))
            {
                return false;
            }

            return true;
        }

        protected override bool TryAnalyzeVariableDeclaration(SyntaxNode declaration, SemanticModel semanticModel, OptionSet optionSet, CancellationToken cancellationToken, out TextSpan issueSpan)
        {
            var variableDeclaration = declaration as VariableDeclarationSyntax;
            Contract.Requires(variableDeclaration != null, "given declaration is not a valid Variable Declaration");

            var typeName = variableDeclaration.Type;
            issueSpan = default(TextSpan);

            // If it is already var, return.
            if (typeName.IsTypeInferred(semanticModel))
            {
                return false;
            }

            var candidateReplacementNode = SyntaxFactory.IdentifierName("var")
                                                .WithLeadingTrivia(typeName.GetLeadingTrivia())
                                                .WithTrailingTrivia(typeName.GetTrailingTrivia());

            var candidateIssueSpan = typeName.Span;

            // If there exists a type named var, return.
            var conflict = semanticModel.GetSpeculativeSymbolInfo(typeName.SpanStart, candidateReplacementNode, SpeculativeBindingOption.BindAsTypeOrNamespace).Symbol;
            if (conflict?.IsKind(SymbolKind.NamedType) == true)
            {
                return false;
            }

            if (typeName.Parent.IsKind(SyntaxKind.VariableDeclaration) &&
                typeName.Parent.IsParentKind(SyntaxKind.LocalDeclarationStatement, SyntaxKind.ForStatement, SyntaxKind.UsingStatement))
            {
                // implicitly typed variables cannot be constants.
                if ((variableDeclaration.Parent as LocalDeclarationStatementSyntax)?.IsConst == true)
                {
                    return false;
                }

                var variable = variableDeclaration.Variables.Single();
                if (AssignmentSupportsStylePreference(variable.Identifier, typeName, variable.Initializer, semanticModel, optionSet, cancellationToken))
                {
                    issueSpan = candidateIssueSpan;
                }
            }
            else if (typeName.IsParentKind(SyntaxKind.ForEachStatement))
            {
                issueSpan = candidateIssueSpan;
            }

            return issueSpan != default(TextSpan);
        }

        /// <summary>
        /// Analyzes the assignment expression and rejects a given declaration if it is unsuitable for implicit typing.
        /// </summary>
        /// <returns>
        /// false, if implicit typing cannot be used.
        /// true, otherwise.
        /// </returns>
        private bool AssignmentSupportsStylePreference(SyntaxToken identifier, TypeSyntax typeName, EqualsValueClauseSyntax initializer, SemanticModel semanticModel, OptionSet optionSet, CancellationToken cancellationToken)
        {
            var expression = initializer.Value;

            // var cannot be assigned null
            if (expression.IsKind(SyntaxKind.NullLiteralExpression))
            {
                return false;
            }

            // cannot use implicit typing on method group, anonymous function or on dynamic
            var declaredType = semanticModel.GetTypeInfo(typeName, cancellationToken).Type;
            if (declaredType != null
                && (declaredType.TypeKind == TypeKind.Delegate
                || declaredType.TypeKind == TypeKind.Dynamic))
            {
                return false;
            }

            // variables declared using var cannot be used further in the same initialization expression.
            if (initializer.DescendantNodesAndSelf()
                    .Where(n => (n as IdentifierNameSyntax)?.Identifier.ValueText.Equals(identifier.ValueText) == true)
                    .Any(n => semanticModel.GetSymbolInfo(n, cancellationToken).Symbol?.IsKind(SymbolKind.Local) == true))
            {
                return false;
            }

            // Get the conversion that occurred between the expression's type and type implied by the expression's context
            // and filter out implicit conversions. If an implicit conversion (other than identity) exists
            // and if we're replacing the declaration with 'var' we'd be changing the semantics by inferring type of
            // initializer expression and thereby losing the conversion.
            var conversion = semanticModel.GetConversion(expression, cancellationToken);
            if (conversion.Exists && conversion.IsImplicit && !conversion.IsIdentity)
            {
                return false;
            }

            // final check to compare type information on both sides of assignment.
            var initializerType = semanticModel.GetTypeInfo(expression, cancellationToken).Type;
            return declaredType.Equals(initializerType);
        }
    }
}