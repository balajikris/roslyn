// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.CodeActions.MoveType
{
    public partial class MoveTypeTests : CSharpMoveTypeTestsBase
    {
        [WpfFact, Trait(Traits.Feature, Traits.Features.CodeActionsMoveType)]
        public async Task SingleClassInFile()
        {
            var code =
@"[||]class Class1 { }";

            var expectedDocumentName = "Class1.cs";

            await TestRenameFileToMatchTypeAsync(code, expectedDocumentName);

            var codeWithTypeRenamedToMatchFileName =
@"class test1 { }";

            await TestRenameTypeToMatchFileAsync(code, codeWithTypeRenamedToMatchFileName);
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.CodeActionsMoveType)]
        public async Task TypeNameMatchesFileName()
        {
            // testworkspace creates files like test1.cs, test2.cs and so on.. 
            // so type name matches filename here and rename file action should not be offered.
            var code =
@"[||]class test1 { }";

            await TestRenameFileToMatchTypeAsync(code, expectedCodeAction: false);
            await TestRenameTypeToMatchFileAsync(code, expectedCodeAction: false);
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.CodeActionsMoveType)]
        public async Task MoreThanOneTypeInFile()
        {
            var code =
@"[||]class Class1 { }
class Class2 { }";

            await TestRenameFileToMatchTypeAsync(code, expectedCodeAction: false);
            await TestRenameTypeToMatchFileAsync(code, expectedCodeAction: false);
        }
    }
}
