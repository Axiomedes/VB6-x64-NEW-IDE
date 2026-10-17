using System.Collections.Generic;

namespace ViBa7.ProjectModel.Advanced;

public class VB6Block
{
    public int StartLine { get; set; }
    public int EndLine { get; set; }
    public int StartColumn { get; set; }
    public string BlockType { get; set; } = string.Empty;
    public string Header { get; set; } = string.Empty;
    public VB6Block? Parent { get; set; }
    public List<VB6Block> Children { get; set; } = [];
    public List<int> IntermediateLines { get; set; } = [];

    public override string ToString() => $"{BlockType} (L{StartLine}-L{EndLine}, Col {StartColumn}): {Header}";
}
