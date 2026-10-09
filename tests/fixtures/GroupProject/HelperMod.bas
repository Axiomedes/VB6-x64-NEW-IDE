Attribute VB_Name = "HelperMod"
Option Explicit

Public Function FormatCustomerName(ByVal rawName As String) As String
    FormatCustomerName = Trim$(UCase$(rawName))
End Function
