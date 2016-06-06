// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.CodeRefactorings.MoveTypeToFile;
using Microsoft.CodeAnalysis.CSharp.Test.Utilities;
using Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.CodeRefactorings;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.CodeActions.MoveTypeToFile
{
    // TODO: should probably be in EditorServicesTEst2 so we can use xml literals
    public class MoveTypeToFileTests : AbstractCSharpCodeActionTest
    {
        private const string SpanMarker = "[||]";

        protected override object CreateCodeRefactoringProvider(Workspace workspace)
        {
            return new MoveTypeToFileCodeRefactoringProvider();
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.CodeActionsMoveType)]
        public async Task ClassWithNoContainerNamespace()
        {
            var code =
@"[||]class C { }";

            var expected = StripSpanMarkers(code);

            await TestAddDocument(code, expected, expectedContainers: Array.Empty<string>(), expectedDocumentName: "C.cs");
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.CodeActionsMoveType)]
        public async Task ClassWithContainerNamespace()
        {
            var code =
@"namespace N1
{
    [||]class C { }
}";

            var expected = StripSpanMarkers(code);

            await TestAddDocument(code, expected, expectedContainers: Array.Empty<string>(), expectedDocumentName: "C.cs");
        }

        private string StripSpanMarkers(string text)
        {
            var index = text.IndexOf(SpanMarker);
            return text.Remove(index, SpanMarker.Length);
        }
    }
}
