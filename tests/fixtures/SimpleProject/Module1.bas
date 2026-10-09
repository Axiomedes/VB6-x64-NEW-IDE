Attribute VB_Name = "Module1"
Option Explicit

Public Sub Main()
    MsgBox "Hola desde VB6!", vbInformation, "VB7x64 Test"
End Sub

Public Function AddNumbers(ByVal a As Long, ByVal b As Long) As Long
    ' Retorna la suma de dos enteros largos
    AddNumbers = a + b
End Function
