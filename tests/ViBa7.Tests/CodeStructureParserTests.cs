using System.Linq;
using ViBa7.ProjectModel.Navigation;
using Xunit;

namespace ViBa7.Tests;

public class CodeStructureParserTests
{
    [Fact]
    public void ParseMembers_ExtractsProceduresCorrectly()
    {
        string code = """
            Option Explicit

            Public Sub InitializeApp(ByVal flag As Boolean)
                Dim localVar As Integer
                localVar = 10
            End Sub

            Private Function Calculate(a As Long, b As Long) As Double
                Calculate = a + b
            End Function

            Public Property Get Total() As Currency
                Total = 100
            End Property

            Public Property Let Total(ByVal value As Currency)
            End Property
            """;

        var members = CodeStructureParser.ParseMembers(code, "Module1.bas");

        Assert.Equal(4, members.Count);

        Assert.Equal("InitializeApp", members[0].Name);
        Assert.Equal(CodeMemberKind.Sub, members[0].Kind);
        Assert.Equal("Public", members[0].Scope);
        Assert.Equal(3, members[0].LineNumber);

        Assert.Equal("Calculate", members[1].Name);
        Assert.Equal(CodeMemberKind.Function, members[1].Kind);
        Assert.Equal("Private", members[1].Scope);
        Assert.Equal("Double", members[1].TypeName);
        Assert.Equal(8, members[1].LineNumber);

        Assert.Equal("Total", members[2].Name);
        Assert.Equal(CodeMemberKind.PropertyGet, members[2].Kind);
        Assert.Equal(12, members[2].LineNumber);

        Assert.Equal("Total", members[3].Name);
        Assert.Equal(CodeMemberKind.PropertyLet, members[3].Kind);
        Assert.Equal(16, members[3].LineNumber);
    }

    [Fact]
    public void ParseMembers_ExtractsModuleLevelDeclarationsAndIgnoresLocals()
    {
        string code = """
            Option Explicit

            Public Const APP_TITLE As String = "My App"
            Private Const MAX_LIMIT = 500
            Private m_Name As String
            Public GlobalCounter As Long

            Public Event OnDataChanged(ByVal newId As Long)

            Public Enum StateEnum
                StateIdle = 0
                StateRunning = 1
            End Enum

            Public Type CustomerRecord
                Id As Long
                FullName As String * 50
            End Type

            Public Sub DoWork()
                Dim insideVar As String
                Const LOCAL_CONST = 10
            End Sub
            """;

        var members = CodeStructureParser.ParseMembers(code, "Class1.cls");

        // Debería tener: APP_TITLE, MAX_LIMIT, m_Name, GlobalCounter, OnDataChanged, StateEnum, CustomerRecord, DoWork
        // Y NO insideVar ni LOCAL_CONST
        Assert.Equal(8, members.Count);

        var appTitle = members.First(m => m.Name == "APP_TITLE");
        Assert.Equal(CodeMemberKind.Constant, appTitle.Kind);
        Assert.Equal("Public", appTitle.Scope);
        Assert.Equal("String", appTitle.TypeName);

        var maxLimit = members.First(m => m.Name == "MAX_LIMIT");
        Assert.Equal(CodeMemberKind.Constant, maxLimit.Kind);
        Assert.Equal("Private", maxLimit.Scope);

        var mName = members.First(m => m.Name == "m_Name");
        Assert.Equal(CodeMemberKind.Variable, mName.Kind);
        Assert.Equal("Private", mName.Scope);
        Assert.Equal("String", mName.TypeName);

        var counter = members.First(m => m.Name == "GlobalCounter");
        Assert.Equal(CodeMemberKind.Variable, counter.Kind);
        Assert.Equal("Public", counter.Scope);
        Assert.Equal("Long", counter.TypeName);

        var ev = members.First(m => m.Name == "OnDataChanged");
        Assert.Equal(CodeMemberKind.Event, ev.Kind);
        Assert.Equal("Public", ev.Scope);

        var en = members.First(m => m.Name == "StateEnum");
        Assert.Equal(CodeMemberKind.Enum, en.Kind);

        var ut = members.First(m => m.Name == "CustomerRecord");
        Assert.Equal(CodeMemberKind.UserType, ut.Kind);

        var subWork = members.First(m => m.Name == "DoWork");
        Assert.Equal(CodeMemberKind.Sub, subWork.Kind);

        Assert.DoesNotContain(members, m => m.Name == "insideVar");
        Assert.DoesNotContain(members, m => m.Name == "LOCAL_CONST");
    }

    [Fact]
    public void ParseMembers_HandlesEmptyOrWhitespace()
    {
        var members = CodeStructureParser.ParseMembers("", "Empty.bas");
        Assert.Empty(members);

        members = CodeStructureParser.ParseMembers("   \r\n \n", "Empty.bas");
        Assert.Empty(members);
    }
}
