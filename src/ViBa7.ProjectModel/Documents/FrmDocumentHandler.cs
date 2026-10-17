using System;

namespace ViBa7.ProjectModel.Documents;

public record FrmSections(
    string HeaderAndDesign,
    string Code,
    bool IsSafelyDelimited,
    string? WarningMessage = null
);

public static class FrmDocumentHandler
{
    public static FrmSections SplitFrmContent(string fullText)
    {
        var sections = VbHeaderHandler.SplitFileContent(fullText, ".frm");
        return new FrmSections(
            HeaderAndDesign: sections.Header,
            Code: sections.Code,
            IsSafelyDelimited: sections.IsSafelyDelimited,
            WarningMessage: sections.WarningMessage
        );
    }

    public static string CombineFrmContent(string headerAndDesign, string newCode, NewLineConvention convention = NewLineConvention.CrLf)
    {
        return VbHeaderHandler.CombineContent(headerAndDesign, newCode, convention);
    }
}
