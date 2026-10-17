namespace ViBa7.ProjectModel.Vbp;

public class ProjectReference
{
    public string RawLine { get; set; } = string.Empty;
    public string Guid { get; set; } = string.Empty;
    public string MajorVersion { get; set; } = "1";
    public string MinorVersion { get; set; } = "0";
    public string Lcid { get; set; } = "0";
    public string FilePath { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public string DisplayTitle => string.IsNullOrWhiteSpace(Description)
        ? (!string.IsNullOrWhiteSpace(FilePath) ? FilePath : Guid)
        : Description;
}
