using System;
using System.IO;
using System.Windows;
using VB7x64.ProjectModel.Analysis;

namespace VB7x64.IDE.Dialogs;

public partial class FileDiffDialog : Window
{
    private readonly Action? _onSaveAction;

    public FileDiffDialog(string filePath, string oldDiskContent, string newEditorContent, Action? onSaveAction = null)
    {
        InitializeComponent();
        _onSaveAction = onSaveAction;

        string fileName = Path.GetFileName(filePath);
        if (string.IsNullOrEmpty(fileName)) fileName = "Documento";

        TxtFileName.Text = $"Archivo: {fileName}";
        TxtFilePath.Text = string.IsNullOrEmpty(filePath) ? "(Sin ruta guardada)" : filePath;

        bool diskExists = !string.IsNullOrEmpty(filePath) && File.Exists(filePath);
        if (!diskExists)
        {
            TxtStatus.Text = "Estado: Archivo nuevo en memoria (aún no existe en disco)";
        }
        else if (string.Equals(oldDiskContent, newEditorContent, StringComparison.Ordinal))
        {
            TxtStatus.Text = "Estado: Idéntico al archivo en disco (sin cambios pendientes)";
        }
        else
        {
            TxtStatus.Text = "Estado: Modificado respecto al archivo en disco";
        }

        // Ejecutar comparación
        var diffResult = DiffEngine.Compare(oldDiskContent, newEditorContent);

        TxtAddedCount.Text = $"+{diffResult.Summary.InsertedCount} agregadas";
        TxtDeletedCount.Text = $"-{diffResult.Summary.DeletedCount} eliminadas";
        TxtModifiedCount.Text = $"~{diffResult.Summary.ModifiedCount} modificadas";

        TxtSummaryFooter.Text = $"Total de diferencias: {diffResult.Summary.TotalChanges} líneas cambiadas";

        LstDiff.ItemsSource = diffResult.Lines;

        if (_onSaveAction == null)
        {
            BtnSaveNow.Visibility = Visibility.Collapsed;
        }
    }

    private void BtnSaveNow_Click(object sender, RoutedEventArgs e)
    {
        _onSaveAction?.Invoke();
        DialogResult = true;
    }
}
