using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeRefactorings.MoveType;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Shared.Options;
using Microsoft.CodeAnalysis.Formatting;

namespace Microsoft.CodeAnalysis.CSharp.CodeRefactorings.MoveType
{
    [ExportLanguageService(typeof(IMoveTypeService), LanguageNames.CSharp), Shared]
    internal class CSharpMoveTypeService :
        AbstractMoveTypeService<CSharpMoveTypeService, BaseTypeDeclarationSyntax, NamespaceDeclarationSyntax, MemberDeclarationSyntax>
    {
    }
}
