using System.Linq;
using ViBa7.ProjectModel.CodeBuilders;
using Xunit;

namespace ViBa7.Tests;

public class CodeBuildersTests
{
    [Fact]
    public void PropertyBuilder_GeneratesPrimitiveGetAndLetCorrectly()
    {
        var options = new PropertyBuilderOptions
        {
            PropertyName = "Precio",
            DataType = "Currency",
            BackingFieldName = "m_Precio",
            Scope = "Public",
            GenerateGet = true,
            GenerateLet = true,
            GenerateSet = false,
            GenerateBackingField = true,
            IncludeComments = true
        };

        string code = PropertyBuilderHelper.GeneratePropertyCode(options);

        Assert.Contains("Private m_Precio As Currency", code);
        Assert.Contains("Public Property Get Precio() As Currency", code);
        Assert.Contains("Precio = m_Precio", code);
        Assert.Contains("Public Property Let Precio(ByVal value As Currency)", code);
        Assert.Contains("m_Precio = value", code);
        Assert.DoesNotContain("Set Precio =", code);
    }

    [Fact]
    public void PropertyBuilder_GeneratesObjectGetAndSetCorrectly()
    {
        var options = new PropertyBuilderOptions
        {
            PropertyName = "Conexion",
            DataType = "ADODB.Connection",
            BackingFieldName = "m_Conn",
            Scope = "Public",
            GenerateGet = true,
            GenerateLet = false,
            GenerateSet = true,
            GenerateBackingField = true
        };

        string code = PropertyBuilderHelper.GeneratePropertyCode(options);

        Assert.Contains("Private m_Conn As ADODB.Connection", code);
        Assert.Contains("Public Property Get Conexion() As ADODB.Connection", code);
        Assert.Contains("Set Conexion = m_Conn", code);
        Assert.Contains("Public Property Set Conexion(ByVal value As ADODB.Connection)", code);
        Assert.Contains("Set m_Conn = value", code);
        Assert.DoesNotContain("Property Let", code);
    }

    [Fact]
    public void ProcedureBuilder_GeneratesSubWithParametersAndErrorHandler()
    {
        var options = new ProcedureBuilderOptions
        {
            ProcedureName = "CargarDatos",
            Kind = BuilderProcedureKind.Sub,
            Scope = "Public",
            IncludeErrorHandler = true,
            Parameters =
            [
                new() { Name = "id", PassingMechanism = "ByVal", DataType = "Long" },
                new() { Name = "nombre", PassingMechanism = "ByRef", DataType = "String" }
            ]
        };

        string code = ProcedureBuilderHelper.GenerateProcedureCode(options);

        Assert.Contains("Public Sub CargarDatos(ByVal id As Long, ByRef nombre As String)", code);
        Assert.Contains("On Error GoTo ErrorHandler", code);
        Assert.Contains("Exit Sub", code);
        Assert.Contains("ErrorHandler:", code);
        Assert.Contains("MsgBox", code);
        Assert.Contains("End Sub", code);
    }

    [Fact]
    public void ProcedureBuilder_GeneratesFunctionWithOptionalParameter()
    {
        var options = new ProcedureBuilderOptions
        {
            ProcedureName = "CalcularDescuento",
            Kind = BuilderProcedureKind.Function,
            Scope = "Private",
            ReturnType = "Double",
            IncludeErrorHandler = false,
            Parameters =
            [
                new() { Name = "monto", PassingMechanism = "ByVal", DataType = "Double" },
                new() { Name = "tasa", PassingMechanism = "ByVal", DataType = "Double", IsOptional = true, DefaultValue = "0.1" }
            ]
        };

        string code = ProcedureBuilderHelper.GenerateProcedureCode(options);

        Assert.Contains("Private Function CalcularDescuento(ByVal monto As Double, Optional ByVal tasa As Double = 0.1) As Double", code);
        Assert.DoesNotContain("On Error GoTo ErrorHandler", code);
        Assert.Contains("End Function", code);
    }

    [Fact]
    public void SnippetManager_ExpandsSnippetsWithIndentation()
    {
        bool success = SnippetManager.TryExpandSnippet("ife", "    ", out string expanded, out int caretOffset);

        Assert.True(success);
        Assert.Contains("If ", expanded);
        Assert.Contains("    Else", expanded);
        Assert.Contains("    End If", expanded);
        Assert.True(caretOffset > 0);
    }

    [Fact]
    public void ClipboardRing_StoresAndRotatesItemsWithoutDuplicates()
    {
        var ring = new ClipboardRing();
        ring.Clear();

        ring.Add("Texto A");
        ring.Add("Texto B");
        ring.Add("Texto A"); // Debería mover "Texto A" al inicio sin duplicar

        Assert.Equal(2, ring.Items.Count);
        Assert.Equal("Texto A", ring.Items[0]);
        Assert.Equal("Texto B", ring.Items[1]);

        // Agregar duplicado consecutivo
        ring.Add("Texto A");
        Assert.Equal(2, ring.Items.Count);
    }
}
