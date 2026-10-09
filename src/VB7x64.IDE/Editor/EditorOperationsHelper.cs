using System;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using VB7x64.ProjectModel.Advanced;

namespace VB7x64.IDE.Editor;

public static class EditorOperationsHelper
{
    public static void CommentSelection(TextEditor editor)
    {
        if (editor.Document == null) return;

        GetAffectedLineRange(editor, out int startLine, out int endLine);

        editor.Document.BeginUpdate();
        try
        {
            for (int lineNum = startLine; lineNum <= endLine; lineNum++)
            {
                var line = editor.Document.GetLineByNumber(lineNum);
                string text = editor.Document.GetText(line.Offset, line.Length);
                string commented = CodeEditorOperations.CommentLine(text);

                if (commented != text)
                {
                    editor.Document.Replace(line.Offset, line.Length, commented);
                }
            }
        }
        finally
        {
            editor.Document.EndUpdate();
        }
    }

    public static void UncommentSelection(TextEditor editor)
    {
        if (editor.Document == null) return;

        GetAffectedLineRange(editor, out int startLine, out int endLine);

        editor.Document.BeginUpdate();
        try
        {
            for (int lineNum = startLine; lineNum <= endLine; lineNum++)
            {
                var line = editor.Document.GetLineByNumber(lineNum);
                string text = editor.Document.GetText(line.Offset, line.Length);
                string uncommented = CodeEditorOperations.UncommentLine(text);

                if (uncommented != text)
                {
                    editor.Document.Replace(line.Offset, line.Length, uncommented);
                }
            }
        }
        finally
        {
            editor.Document.EndUpdate();
        }
    }

    public static void SwitchAssignment(TextEditor editor)
    {
        if (editor.Document == null) return;

        GetAffectedLineRange(editor, out int startLine, out int endLine);

        editor.Document.BeginUpdate();
        try
        {
            for (int lineNum = startLine; lineNum <= endLine; lineNum++)
            {
                var line = editor.Document.GetLineByNumber(lineNum);
                string text = editor.Document.GetText(line.Offset, line.Length);
                string switched = CodeEditorOperations.SwitchAssignmentLine(text);

                if (switched != text)
                {
                    editor.Document.Replace(line.Offset, line.Length, switched);
                }
            }
        }
        finally
        {
            editor.Document.EndUpdate();
        }
    }

    public static void ConvertCase(TextEditor editor, TextCaseMode mode)
    {
        if (editor.Document == null) return;

        if (editor.SelectionLength > 0)
        {
            string selectedText = editor.SelectedText;
            string converted = CodeEditorOperations.ChangeCase(selectedText, mode);
            if (converted != selectedText)
            {
                editor.Document.Replace(editor.SelectionStart, editor.SelectionLength, converted);
            }
        }
        else
        {
            // Si no hay selección, convertir la palabra o la línea actual
            var line = editor.Document.GetLineByNumber(editor.TextArea.Caret.Line);
            string lineText = editor.Document.GetText(line.Offset, line.Length);
            string converted = CodeEditorOperations.ChangeCase(lineText, mode);
            if (converted != lineText)
            {
                editor.Document.Replace(line.Offset, line.Length, converted);
            }
        }
    }

    private static void GetAffectedLineRange(TextEditor editor, out int startLine, out int endLine)
    {
        if (editor.SelectionLength > 0)
        {
            int startOffset = editor.SelectionStart;
            int endOffset = editor.SelectionStart + editor.SelectionLength;

            startLine = editor.Document.GetLineByOffset(startOffset).LineNumber;
            var endDocLine = editor.Document.GetLineByOffset(endOffset);
            endLine = endDocLine.LineNumber;

            // Si la selección termina exactamente al inicio de una línea siguiente, excluirla
            if (endOffset == endDocLine.Offset && endLine > startLine)
            {
                endLine--;
            }
        }
        else
        {
            startLine = editor.TextArea.Caret.Line;
            endLine = startLine;
        }
    }
}
