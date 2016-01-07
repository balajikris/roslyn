﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Text;
using System.Diagnostics;
using System.Collections.Generic;

namespace Microsoft.CodeAnalysis.CSharp.Diagnostics.UseImplicitTyping
{
    /* TODO :
    *   1. pipe through options
    *   2. Design an options page to support tweaks to settings
    *       e.g: use var 'except' on primitive types, do not use var 'except' when type is apparent from rhs.
    *   3. Refactoring to common base class.
    *       a. UseImplicitType and UseExplicitType : AbstractCSharpUseTypingStyle
    *       b. CSharp and VB implementations to AbstractUseTypingStyle
    */

    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    internal sealed class CSharpUseExplicitTypingDiagnosticAnalyzer : DiagnosticAnalyzer, IBuiltInAnalyzer
    {
        // TODO: 
        // 1. localize title and message
        // 2. tweak severity and custom tags 
        //      a. need to have various levels of diagnostics to report based on option settings.
        private static readonly DiagnosticDescriptor s_descriptorUseImplicitTyping = new DiagnosticDescriptor(
            id: IDEDiagnosticIds.UseExplicitTypingDiagnosticId,
            title: "Use explicit typing",
            messageFormat: "Use type name instead of var",
            category: DiagnosticCategory.Style,
            defaultSeverity: DiagnosticSeverity.Info,
            isEnabledByDefault: true,
            customTags: DiagnosticCustomTags.Unnecessary);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(s_descriptorUseImplicitTyping);

        public DiagnosticAnalyzerCategory GetAnalyzerCategory()
        {
            return DiagnosticAnalyzerCategory.SemanticSpanAnalysis;
        }

        public override void Initialize(AnalysisContext context)
        {
            // TODO: check for generatedcode and bail.
            // context.ConfigureGeneratedCodeAnalysis() See https://github.com/dotnet/roslyn/pull/7526

            context.RegisterSyntaxNodeAction(HandleVariableDeclaration, SyntaxKind.VariableDeclaration);
            context.RegisterSyntaxNodeAction(HandleForEachStatement, SyntaxKind.ForEachStatement);
        }

        private void HandleVariableDeclaration(SyntaxNodeAnalysisContext context)
        {
            var variableDeclaration = (VariableDeclarationSyntax)context.Node;

            // var is applicable only for local variables.
            if (variableDeclaration.Parent.IsKind(SyntaxKind.FieldDeclaration) ||
                variableDeclaration.Parent.IsKind(SyntaxKind.EventFieldDeclaration))
            {
                return;
            }

            // implicitly typed variables cannot have multiple declarators and they
            // must have an initializer. Anything that is not of that form is an error case.
            if (variableDeclaration.Variables.Count > 1 ||
                !variableDeclaration.Variables.Single().Initializer.IsKind(SyntaxKind.EqualsValueClause))
            {
                return;
            }

            // TODO: Check options and bail.
            var optionSet = GetOptionSet(context.Options);
            Debug.Assert(variableDeclaration.Variables.Count == 1, "More than 1 variable declared, var is not legal here.");
            var diagnostic = AnalyzeVariableDeclaration(variableDeclaration, context.SemanticModel, context.CancellationToken);

            if (diagnostic != null)
            {
                context.ReportDiagnostic(diagnostic);
            }
        }

        private void HandleForEachStatement(SyntaxNodeAnalysisContext context)
        {
            var forEachStatement = (ForEachStatementSyntax)context.Node;
            var diagnostic = AnalyzeVariableDeclaration(forEachStatement, context.SemanticModel, context.CancellationToken);

            // TODO: Check options and bail.
            if (diagnostic != null)
            {
                context.ReportDiagnostic(diagnostic);
            }
        }

        private Diagnostic AnalyzeVariableDeclaration(SyntaxNode declarationStatement,
            SemanticModel semanticModel,
            CancellationToken cancellationToken)
        {
            TextSpan diagnosticSpan;
            TypeSyntax declaredType;

            if (declarationStatement.IsKind(SyntaxKind.VariableDeclaration))
            {
                declaredType = ((VariableDeclarationSyntax)declarationStatement).Type;
            }
            else if (declarationStatement.IsKind(SyntaxKind.ForEachStatement))
            {
                declaredType = ((ForEachStatementSyntax)declarationStatement).Type;
            }
            else
            {
                Debug.Assert(false, $"unhandled kind {declarationStatement.Kind().ToString()}");
                return null;
            }

            var isReplaceable = IsVarReplaceable(declaredType, semanticModel, cancellationToken, out diagnosticSpan);

            return isReplaceable
                ? CreateDiagnostic(declarationStatement, diagnosticSpan)
                : null;
        }

        private static Diagnostic CreateDiagnostic(SyntaxNode declarationStatement, TextSpan diagnosticSpan)
            => Diagnostic.Create(s_descriptorUseImplicitTyping, declarationStatement.SyntaxTree.GetLocation(diagnosticSpan));

        private bool IsVarReplaceable(TypeSyntax typeName, SemanticModel semanticModel, CancellationToken cancellationToken, out TextSpan issueSpan)
        {
            issueSpan = default(TextSpan);

            // If it is currently not var, explicit typing exists, return. 
            // this also takes care of cases where var is mapped to a named type via an alias or a class declaration.
            if (!typeName.IsTypeInferred(semanticModel))
            {
                return false;
            }

            if (typeName.Parent.IsKind(SyntaxKind.VariableDeclaration) && 
                typeName.Parent.Parent.IsKind(SyntaxKind.LocalDeclarationStatement, SyntaxKind.ForStatement, SyntaxKind.UsingStatement))
            {
                // check assignment for variable declarations.
                var variable = ((VariableDeclarationSyntax)typeName.Parent).Variables.First();
                if (!CheckAssignment(variable.Identifier, typeName, variable.Initializer, semanticModel, cancellationToken))
                {
                    return false;
                }
            }

            issueSpan = typeName.Span;
            return true;
        }

        private bool CheckAssignment(SyntaxToken identifier, TypeSyntax typeName, EqualsValueClauseSyntax initializer, SemanticModel semanticModel, CancellationToken cancellationToken)
        {
            // is or contains an anonymous type
            // cases :
            //        var anon = new { Num = 1 };
            //        var enumerableOfAnons = from prod in products select new { prod.Color, prod.Price };
            var declaredType = semanticModel.GetTypeInfo(typeName, cancellationToken).Type;
            if (declaredType.ContainsAnonymousType())
            {
                return false;
            }

            // cannot find type if initializer resolves to an ErrorTypeSymbol
            var initializerTypeInfo = semanticModel.GetTypeInfo(initializer.Value, cancellationToken);
            var initializerType = initializerTypeInfo.Type;
            if (initializerType.IsErrorType())
            {
                return false;
            }

            return true;
        }

        private OptionSet GetOptionSet(AnalyzerOptions analyzerOptions)
        {
            var workspaceOptions = analyzerOptions as WorkspaceAnalyzerOptions;
            if (workspaceOptions != null)
            {
                return workspaceOptions.Workspace.Options;
            }

            return null;
        }
    }
}