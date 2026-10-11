using System;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using VB7x64.ProjectModel.Compilation;
using VB7x64.ProjectModel.Vbp;

namespace VB7x64.IDE.Dialogs;

/// <summary>
/// Diálogo para compilar el proyecto actual a un archivo binario nativo x32 (PE32) enlazado a MSVBVM60.DLL.
/// </summary>
public partial class MakeProjectExeDialog : Window
{
    private readonly VB6Project _project;

    /// <summary>
    /// Resultado de la compilación si fue exitosa.
    /// </summary>
    public CompileResult? CompileResult { get; private set; }

    public MakeProjectExeDialog(VB6Project project)
    {
        InitializeComponent();
        _project = project ?? throw new ArgumentNullException(nameof(project));

        TxtHeaderProject.Text = $"Generar {(!string.IsNullOrWhiteSpace(_project.Name) ? _project.Name : "Proyecto1")}.exe";

        // Determinar directorio y nombre por defecto
        string baseDir = !string.IsNullOrWhiteSpace(_project.ProjectDirectory)
            ? _project.ProjectDirectory
            : (!string.IsNullOrWhiteSpace(_project.ProjectFilePath) ? Path.GetDirectoryName(_project.ProjectFilePath) ?? "" : Environment.CurrentDirectory);

        string exeName = !string.IsNullOrWhiteSpace(_project.ExeName32)
            ? _project.ExeName32
            : $"{(!string.IsNullOrWhiteSpace(_project.Name) ? _project.Name : "Proyecto1")}.exe";

        if (!exeName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            exeName += ".exe";
        }

        TxtOutputPath.Text = Path.Combine(baseDir, exeName);

        // Cargar metadatos
        TxtTitle.Text = !string.IsNullOrWhiteSpace(_project.Title) ? _project.Title : _project.Name;
        TxtMajor.Text = _project.MajorVer.ToString();
        TxtMinor.Text = _project.MinorVer.ToString();
        TxtRevision.Text = _project.RevisionVer.ToString();
        ChkAutoIncrement.IsChecked = _project.AutoIncrementVer;

        TxtDescription.Text = _project.FileDescription;
        TxtCompany.Text = _project.CompanyName;
        TxtCopyright.Text = _project.LegalCopyright;
    }

    private void BtnBrowse_Click(object sender, RoutedEventArgs e)
    {
        var sfd = new SaveFileDialog
        {
            Title = "Seleccionar ubicación del archivo ejecutable",
            Filter = "Archivos Ejecutables (*.exe)|*.exe|Todos los archivos (*.*)|*.*",
            DefaultExt = ".exe"
        };

        try
        {
            if (!string.IsNullOrWhiteSpace(TxtOutputPath.Text))
            {
                string? dir = Path.GetDirectoryName(TxtOutputPath.Text);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                {
                    sfd.InitialDirectory = dir;
                }
                sfd.FileName = Path.GetFileName(TxtOutputPath.Text);
            }
        }
        catch
        {
            // Fallback a diálogo predeterminado
        }

        if (sfd.ShowDialog(this) == true)
        {
            TxtOutputPath.Text = sfd.FileName;
        }
    }

    private void BtnGenerate_Click(object sender, RoutedEventArgs e)
    {
        string outputPath = TxtOutputPath.Text.Trim();
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            MessageBox.Show("Por favor especifique la ruta de destino del archivo ejecutable.", "Ruta no válida", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtOutputPath.Focus();
            return;
        }

        if (!outputPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            outputPath += ".exe";
            TxtOutputPath.Text = outputPath;
        }

        // Parsear versiones
        int.TryParse(TxtMajor.Text.Trim(), out int major);
        int.TryParse(TxtMinor.Text.Trim(), out int minor);
        int.TryParse(TxtRevision.Text.Trim(), out int revision);

        if (ChkAutoIncrement.IsChecked == true)
        {
            revision++;
            TxtRevision.Text = revision.ToString();
        }

        // Actualizar metadatos del proyecto en memoria
        _project.MajorVer = Math.Max(0, major);
        _project.MinorVer = Math.Max(0, minor);
        _project.RevisionVer = Math.Max(0, revision);
        _project.AutoIncrementVer = ChkAutoIncrement.IsChecked == true;
        _project.Title = TxtTitle.Text.Trim();
        _project.FileDescription = TxtDescription.Text.Trim();
        _project.CompanyName = TxtCompany.Text.Trim();
        _project.LegalCopyright = TxtCopyright.Text.Trim();
        _project.ExeName32 = Path.GetFileName(outputPath);

        // Ejecutar compilación nativa
        var compiler = new Vb6ProjectCompiler();
        var result = compiler.Compile(_project, outputPath);

        if (result.Success)
        {
            CompileResult = result;
            DialogResult = true;
            Close();
        }
        else
        {
            string errorMsg = string.Join(Environment.NewLine, result.Messages);
            if (result.Error != null)
            {
                errorMsg += $"{Environment.NewLine}{Environment.NewLine}Detalles: {result.Error.Message}";
            }

            MessageBox.Show($"Ocurrió un error al generar el ejecutable:\n\n{errorMsg}", "Error de compilación", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
