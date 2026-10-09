using System;
using System.IO;
using System.Text;
using VB7x64.ProjectModel.Documents;

namespace VB7x64.ProjectModel.Vbp;

public static class VbpParser
{
    public static VB6Project Parse(string vbpFilePath, Encoding? fallbackEncoding = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(vbpFilePath);

        if (!File.Exists(vbpFilePath))
        {
            throw new FileNotFoundException($"El archivo de proyecto no existe: {vbpFilePath}", vbpFilePath);
        }

        string projectDirectory = Path.GetDirectoryName(vbpFilePath) ?? string.Empty;
        var doc = SafeFileStorage.LoadDocument(vbpFilePath, fallbackEncoding);

        var project = new VB6Project
        {
            ProjectFilePath = Path.GetFullPath(vbpFilePath),
            Name = Path.GetFileNameWithoutExtension(vbpFilePath)
        };

        using var reader = new StringReader(doc.Text);
        string? line;
        int lineNumber = 0;

        while ((line = reader.ReadLine()) != null)
        {
            lineNumber++;
            project.RawLines.Add(line);

            string trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;

            int eqIndex = trimmed.IndexOf('=');
            if (eqIndex <= 0)
            {
                // Línea sin clave=valor, se almacena como desconocida
                project.Items.Add(new ProjectItem
                {
                    LineNumber = lineNumber,
                    RawLine = line,
                    Type = ProjectItemType.Unknown
                });
                continue;
            }

            string key = trimmed[..eqIndex].Trim();
            string value = trimmed[(eqIndex + 1)..].Trim();

            // Quitar comillas si las hay
            string cleanValue = value;
            if (cleanValue.StartsWith('"') && cleanValue.EndsWith('"') && cleanValue.Length >= 2)
            {
                cleanValue = cleanValue[1..^1];
            }

            switch (key.ToUpperInvariant())
            {
                case "TYPE":
                    project.ProjectType = cleanValue;
                    project.Settings[key] = cleanValue;
                    break;

                case "NAME":
                    project.Name = cleanValue;
                    project.Settings[key] = cleanValue;
                    break;

                case "STARTUP":
                    project.Startup = cleanValue;
                    project.Settings[key] = cleanValue;
                    break;

                case "TITLE":
                    project.Title = cleanValue;
                    project.Settings[key] = cleanValue;
                    break;

                case "EXENAME32":
                    project.ExeName32 = cleanValue;
                    project.Settings[key] = cleanValue;
                    break;

                case "FORM":
                    ParseFormItem(project, line, lineNumber, value, projectDirectory);
                    break;

                case "MODULE":
                    ParseNamedItem(project, line, lineNumber, value, projectDirectory, ProjectItemType.Module);
                    break;

                case "CLASS":
                    ParseNamedItem(project, line, lineNumber, value, projectDirectory, ProjectItemType.Class);
                    break;

                case "USERCONTROL":
                    ParseFormItem(project, line, lineNumber, value, projectDirectory, ProjectItemType.UserControl);
                    break;

                case "PROPERTYPAGE":
                    ParseFormItem(project, line, lineNumber, value, projectDirectory, ProjectItemType.PropertyPage);
                    break;

                case "USERDOCUMENT":
                    ParseFormItem(project, line, lineNumber, value, projectDirectory, ProjectItemType.UserDocument);
                    break;

                case "DESIGNER":
                    ParseFormItem(project, line, lineNumber, value, projectDirectory, ProjectItemType.Designer);
                    break;

                case "REFERENCE":
                    ParseReferenceItem(project, line, value, projectDirectory);
                    break;

                case "OBJECT":
                    ParseObjectItem(project, line, lineNumber, value, projectDirectory);
                    break;

                default:
                    // Guardar propiedades generales en Settings y como ProjectItem Setting/Unknown
                    project.Settings[key] = cleanValue;
                    project.Items.Add(new ProjectItem
                    {
                        LineNumber = lineNumber,
                        RawLine = line,
                        Type = ProjectItemType.Setting,
                        Name = key
                    });
                    break;
            }
        }

        return project;
    }

    private static void ParseFormItem(VB6Project project, string rawLine, int lineNumber, string value, string projectDir, ProjectItemType type = ProjectItemType.Form)
    {
        string relativePath = value.Trim().Trim('"');
        string absPath = ResolvePath(projectDir, relativePath);

        project.Items.Add(new ProjectItem
        {
            LineNumber = lineNumber,
            RawLine = rawLine,
            Type = type,
            Name = Path.GetFileNameWithoutExtension(relativePath),
            RelativePath = relativePath,
            AbsolutePath = absPath,
            FileExists = File.Exists(absPath)
        });
    }

    private static void ParseNamedItem(VB6Project project, string rawLine, int lineNumber, string value, string projectDir, ProjectItemType type)
    {
        // Formato estándar: Nombre; RutaRelativa
        string name = string.Empty;
        string relativePath = value;

        int semiIndex = value.IndexOf(';');
        if (semiIndex >= 0)
        {
            name = value[..semiIndex].Trim();
            relativePath = value[(semiIndex + 1)..].Trim();
        }

        relativePath = relativePath.Trim('"');
        string absPath = ResolvePath(projectDir, relativePath);

        if (string.IsNullOrEmpty(name))
        {
            name = Path.GetFileNameWithoutExtension(relativePath);
        }

        project.Items.Add(new ProjectItem
        {
            LineNumber = lineNumber,
            RawLine = rawLine,
            Type = type,
            Name = name,
            RelativePath = relativePath,
            AbsolutePath = absPath,
            FileExists = File.Exists(absPath)
        });
    }

    private static void ParseReferenceItem(VB6Project project, string rawLine, string value, string projectDir)
    {
        // Formato: Reference=*\G{GUID}#Major.Minor#LCID#Path#Description
        // Ejemplo: *\G{00020430-0000-0000-C000-000000000046}#2.0#0#..\..\..\stdole2.tlb#OLE Automation
        var refItem = new ProjectReference { RawLine = rawLine };
        string[] parts = value.Split('#');

        if (parts.Length >= 1)
        {
            string guidPart = parts[0];
            int gIdx = guidPart.IndexOf('{');
            int closeIdx = guidPart.IndexOf('}');
            if (gIdx >= 0 && closeIdx > gIdx)
            {
                refItem.Guid = guidPart.Substring(gIdx, closeIdx - gIdx + 1);
            }
            else
            {
                refItem.Guid = guidPart;
            }
        }

        if (parts.Length >= 2)
        {
            string[] verParts = parts[1].Split('.');
            refItem.MajorVersion = verParts.Length > 0 ? verParts[0] : "1";
            refItem.MinorVersion = verParts.Length > 1 ? verParts[1] : "0";
        }

        if (parts.Length >= 3) refItem.Lcid = parts[2];
        if (parts.Length >= 4) refItem.FilePath = ResolvePath(projectDir, parts[3].Trim());
        if (parts.Length >= 5) refItem.Description = parts[4].Trim();

        project.References.Add(refItem);
    }

    private static void ParseObjectItem(VB6Project project, string rawLine, int lineNumber, string value, string projectDir)
    {
        // Formato: {GUID}#1.2#0; Comdlg32.ocx
        string name = string.Empty;
        string relativePath = string.Empty;

        int semi = value.IndexOf(';');
        if (semi >= 0)
        {
            relativePath = value[(semi + 1)..].Trim();
            name = Path.GetFileNameWithoutExtension(relativePath);
        }

        string absPath = !string.IsNullOrEmpty(relativePath) ? ResolvePath(projectDir, relativePath) : string.Empty;

        project.Items.Add(new ProjectItem
        {
            LineNumber = lineNumber,
            RawLine = rawLine,
            Type = ProjectItemType.ActiveXControl,
            Name = name,
            RelativePath = relativePath,
            AbsolutePath = absPath,
            FileExists = string.IsNullOrEmpty(absPath) || File.Exists(absPath)
        });
    }

    public static string ResolvePath(string baseDirectory, string relativeOrAbsolutePath)
    {
        if (string.IsNullOrWhiteSpace(relativeOrAbsolutePath)) return string.Empty;

        if (Path.IsPathRooted(relativeOrAbsolutePath))
        {
            return Path.GetFullPath(relativeOrAbsolutePath);
        }

        return Path.GetFullPath(Path.Combine(baseDirectory, relativeOrAbsolutePath));
    }
}
