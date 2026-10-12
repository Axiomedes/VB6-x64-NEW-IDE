using System;
using System.Collections.Generic;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Folding;
using VB7x64.ProjectModel.Advanced;

namespace VB7x64.IDE.Editor;

/// <summary>
/// Estrategia de plegado y desplegado de código (Code Folding) para Visual Basic 6.
/// Genera regiones colapsables basadas en los bloques analizados por VB6BlockParser
/// (Procedimientos Sub/Function/Property y estructuras de control If, Select, For, Do, While, With).
/// </summary>
public class VB6FoldingStrategy
{
    public void UpdateFoldings(FoldingManager manager, TextDocument document)
    {
        int firstErrorOffset;
        var foldings = CreateNewFoldings(document, out firstErrorOffset);
        manager.UpdateFoldings(foldings, firstErrorOffset);
    }

    public IEnumerable<NewFolding> CreateNewFoldings(TextDocument document, out int firstErrorOffset)
    {
        firstErrorOffset = -1;
        var foldings = new List<NewFolding>();

        if (document == null || document.TextLength == 0)
            return foldings;

        List<VB6Block> blocks;
        try
        {
            blocks = VB6BlockParser.ParseBlocks(document.Text);
        }
        catch
        {
            return foldings;
        }

        foreach (var block in blocks)
        {
            CollectFoldings(block, document, foldings);
        }

        foldings.Sort((a, b) => a.StartOffset.CompareTo(b.StartOffset));
        return foldings;
    }

    private static void CollectFoldings(VB6Block block, TextDocument document, List<NewFolding> foldings)
    {
        if (block.EndLine > block.StartLine && block.StartLine >= 1 && block.EndLine <= document.LineCount)
        {
            var startLine = document.GetLineByNumber(block.StartLine);
            var endLine = document.GetLineByNumber(block.EndLine);

            int startOffset = startLine.Offset + startLine.Length;
            int endOffset = endLine.Offset + endLine.Length;

            if (endOffset > startOffset)
            {
                foldings.Add(new NewFolding(startOffset, endOffset)
                {
                    Name = " ... "
                });
            }
        }

        if (block.Children != null)
        {
            foreach (var child in block.Children)
            {
                CollectFoldings(child, document, foldings);
            }
        }
    }
}
