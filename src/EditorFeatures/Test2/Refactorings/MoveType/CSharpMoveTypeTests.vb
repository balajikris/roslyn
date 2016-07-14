Imports System.Threading.Tasks
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.CodeRefactorings
Imports Microsoft.CodeAnalysis.CSharp.CodeRefactorings.MoveType
Imports Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.CodeRefactorings
Imports Microsoft.CodeAnalysis.Editor.UnitTests.CodeActions
Imports Microsoft.CodeAnalysis.Editor.UnitTests.Workspaces

Namespace Microsoft.CodeAnalysis.Editor.UnitTests.Refactorings.MoveType
    Public Class CSharpMoveTypeTests
        Inherits AbstractCSharpCodeActionTest

        Protected Overrides Function CreateCodeRefactoringProvider(workspace As Workspace) As CodeRefactoringProvider
            Return New MoveTypeCodeRefactoringProvider()
        End Function

        '<Fact(), Trait(Traits.Feature, Traits.Features.CodeActionsMoveType)>
        'Public Async Function ClassWithNoContainerNamespace() As Task
        '    Dim code = "class $$Class1 { }"
        '    Dim workspace = GetWorkspaceWithCode(code)
        'End Function

        Private Function GetWorkspaceWithCode(code As String) As XElement
            Dim workspace =
                <Workspace>
                    <Project Language='C#' AssemblyName='CSharpAssembly1' CommonReferences='true'>
                        <Document FilePath='Test1.cs'>
                            <%= code %>
                        </Document>
                    </Project>
                </Workspace>

            Return workspace
        End Function
    End Class
End Namespace
