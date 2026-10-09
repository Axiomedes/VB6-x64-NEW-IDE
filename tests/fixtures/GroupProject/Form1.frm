VERSION 5.00
Begin VB.Form Form1 
   Caption         =   "App Form"
   ClientHeight    =   3195
   ClientLeft      =   60
   ClientTop       =   345
   ClientWidth     =   4680
   StartUpPosition =   3  'Windows Default
   Begin VB.CommandButton CmdGo 
      Caption         =   "Ejecutar"
      Height          =   495
      Left            =   1200
      Top             =   1080
      Width           =   1815
   End
End
Attribute VB_Name = "Form1"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = False
Attribute VB_PredeclaredId = True
Attribute VB_Exposed = False
Option Explicit

Private Sub CmdGo_Click()
    Dim c As Customer
    Set c = New Customer
    c.Name = "Cliente 1"
    MsgBox "Hola " & c.Name, vbInformation
End Sub

Private Sub Form_Load()
    Caption = "Formulario de Grupo"
End Sub
