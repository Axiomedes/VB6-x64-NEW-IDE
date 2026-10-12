using System;
using VB7x64.ProjectModel.CodeBuilders;
using Xunit;

namespace VB7x64.Tests;

public class VB6LineFormatterTests
{
    [Fact]
    public void FormatLine_AutoCapitalizes_KeywordsAndIntrinsicFunctions()
    {
        // msgbox -> MsgBox
        Assert.Equal("MsgBox \"Hola\"", VB6LineFormatter.FormatLine("msgbox \"Hola\""));

        // iif -> IIf
        Assert.Equal("x = IIf(a = 1, \"si\", \"no\")", VB6LineFormatter.FormatLine("x=iif(a=1,\"si\",\"no\")"));

        // val, str
        Assert.Equal("n = Val(s)", VB6LineFormatter.FormatLine("n=val(s)"));
        Assert.Equal("t = Str(100)", VB6LineFormatter.FormatLine("t=str(100)"));

        // if, then, else, elseif, end if
        Assert.Equal("If a = 1 Then", VB6LineFormatter.FormatLine("if a=1 then"));
        Assert.Equal("ElseIf b = 2 Then", VB6LineFormatter.FormatLine("elseif b=2 then"));
        Assert.Equal("End If", VB6LineFormatter.FormatLine("end if"));

        // for, to, step, next
        Assert.Equal("For i = 1 To 10 Step 2", VB6LineFormatter.FormatLine("for i=1 to 10 step 2"));
        Assert.Equal("Next i", VB6LineFormatter.FormatLine("next i"));

        // do, while, until, loop
        Assert.Equal("Do While x > 0", VB6LineFormatter.FormatLine("do while x>0"));
        Assert.Equal("Loop", VB6LineFormatter.FormatLine("loop"));

        // dim, as, data types
        Assert.Equal("Dim contador As Long", VB6LineFormatter.FormatLine("dim contador as long"));
        Assert.Equal("Dim texto As String, flag As Boolean", VB6LineFormatter.FormatLine("dim texto as string,flag as boolean"));

        // sub, function, end sub
        Assert.Equal("Private Sub Command1_Click()", VB6LineFormatter.FormatLine("private sub Command1_Click()"));
        Assert.Equal("End Sub", VB6LineFormatter.FormatLine("end sub"));
    }

    [Fact]
    public void FormatLine_NormalizesSpacing_AroundOperatorsAndCommas()
    {
        // Asignación con espacios
        Assert.Equal("stVal = \"Texto\"", VB6LineFormatter.FormatLine("stVal=\"Texto\""));

        // Operadores relacionales
        Assert.Equal("If a <= 10 Then", VB6LineFormatter.FormatLine("if a<=10 then"));
        Assert.Equal("If a <> b Then", VB6LineFormatter.FormatLine("if a<>b then"));

        // Comas con espacio tras cada coma
        Assert.Equal("MsgBox \"Hola\", vbCritical, \"Titulo\"", VB6LineFormatter.FormatLine("MsgBox \"Hola\",vbcritical,\"Titulo\""));
        Assert.Equal("a, b, c", VB6LineFormatter.FormatLine("a,b,c"));

        // Expresiones aritméticas
        Assert.Equal("total = x + y * 2", VB6LineFormatter.FormatLine("total=x+y*2"));

        // Signos unarios
        Assert.Equal("a = -1", VB6LineFormatter.FormatLine("a=-1"));
        Assert.Equal("Call Test(-5, +10)", VB6LineFormatter.FormatLine("call Test(-5,+10)"));
    }

    [Fact]
    public void FormatLine_AutoCloses_UnclosedStringQuotes()
    {
        // stVal="Texto -> stVal = "Texto"
        Assert.Equal("stVal = \"Texto\"", VB6LineFormatter.FormatLine("stVal=\"Texto"));

        // MsgBox "Hola mundo -> MsgBox "Hola mundo"
        Assert.Equal("MsgBox \"Hola mundo\"", VB6LineFormatter.FormatLine("msgbox \"Hola mundo"));

        // Text1.Text="Nuevo valor -> Text1.Text = "Nuevo valor"
        Assert.Equal("Text1.Text = \"Nuevo valor\"", VB6LineFormatter.FormatLine("Text1.Text=\"Nuevo valor"));
    }

    [Fact]
    public void FormatLine_PreservesLiteralStringsAndComments_Verbatim()
    {
        // El contenido de la cadena no se altera en mayúsculas ni en espacios
        Assert.Equal("stVal = \"texto en minuscula con = y , sin cambiar\"",
            VB6LineFormatter.FormatLine("stVal=\"texto en minuscula con = y , sin cambiar\""));

        // Comentario puro al inicio
        Assert.Equal("' msgbox \"hola\", a=b", VB6LineFormatter.FormatLine("' msgbox \"hola\", a=b"));
        Assert.Equal("Rem msgbox \"hola\", a=b", VB6LineFormatter.FormatLine("Rem msgbox \"hola\", a=b"));

        // Comentario al final de la línea
        Assert.Equal("stVal = \"Texto\" ' asigna valor", VB6LineFormatter.FormatLine("stVal=\"Texto\" ' asigna valor"));

        // Indentación inicial preservada
        Assert.Equal("    stVal = \"Texto\"", VB6LineFormatter.FormatLine("    stVal=\"Texto\""));
    }

    [Fact]
    public void FormatLine_Handles_MsgBoxDualSyntaxAndParentheses()
    {
        // Sentencia sin paréntesis
        Assert.Equal("MsgBox opcion1, opcion2, opcion3",
            VB6LineFormatter.FormatLine("MSGBOX opcion1,opcion2,opcion3"));

        // Sentencia con comas y opciones
        Assert.Equal("MsgBox \"Error\", vbCritical, \"Aviso\"",
            VB6LineFormatter.FormatLine("msgbox \"Error\",vbcritical,\"Aviso\""));

        // Función con paréntesis asignada a variable (Val es función intrínseca, res es variable de usuario)
        Assert.Equal("res = MsgBox(opcion1, opcion2, opcion3)",
            VB6LineFormatter.FormatLine("res=MSGBOX(opcion1,opcion2,opcion3)"));

        // Función con opciones compuestas
        Assert.Equal("ret = MsgBox(\"¿Desea salir?\", vbQuestion + vbYesNo, \"Confirmar\")",
            VB6LineFormatter.FormatLine("ret=msgbox(\"¿Desea salir?\",vbquestion+vbyesno,\"Confirmar\")"));
    }
}
