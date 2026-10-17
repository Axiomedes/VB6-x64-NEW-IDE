using System;
using System.Text;
using System.Windows;
using ViBa7.ProjectModel.Analysis;

namespace ViBa7.IDE.Dialogs;

public partial class CodeStatisticsDialog : Window
{
    private readonly ProjectStatisticsSummary _summary;

    public CodeStatisticsDialog(ProjectStatisticsSummary summary)
    {
        InitializeComponent();
        _summary = summary;

        TxtProjectHeader.Text = $"Estadísticas de: {summary.ProjectName}";

        TxtTotalSloc.Text = $"{summary.TotalCodeLines:N0}";
        TxtTotalComments.Text = $"{summary.TotalCommentLines:N0}";
        TxtCommentPercent.Text = $"{summary.OverallCommentPercentage}% del total ({summary.TotalLines:N0} líneas)";
        TxtTotalProcedures.Text = $"{summary.TotalProcedures:N0}";
        TxtTotalComponents.Text = $"{summary.TotalFiles:N0}";
        TxtComponentsBreakdown.Text = $"{summary.FormCount} frm, {summary.ModuleCount} bas, {summary.ClassCount} cls, {summary.UserControlCount} ctl";

        DgFiles.ItemsSource = summary.Files;
    }

    private void BtnCopySummary_Click(object sender, RoutedEventArgs e)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=================================================");
        sb.AppendLine($" ESTADÍSTICAS DE CÓDIGO - {_summary.ProjectName}");
        sb.AppendLine($"=================================================");
        sb.AppendLine($"Total de Archivos: {_summary.TotalFiles}");
        sb.AppendLine($"Total de Líneas:   {_summary.TotalLines:N0}");
        sb.AppendLine($"Líneas de Código (SLOC): {_summary.TotalCodeLines:N0}");
        sb.AppendLine($"Líneas de Comentarios:   {_summary.TotalCommentLines:N0} ({_summary.OverallCommentPercentage}%)");
        sb.AppendLine($"Líneas en Blanco:        {_summary.TotalBlankLines:N0}");
        sb.AppendLine($"Total de Procedimientos: {_summary.TotalProcedures:N0}");
        sb.AppendLine($"Desglose de Componentes: {_summary.FormCount} Formularios, {_summary.ModuleCount} Módulos, {_summary.ClassCount} Clases, {_summary.UserControlCount} Controles de Usuario");
        sb.AppendLine();
        sb.AppendLine($"DESGLOSE POR ARCHIVO:");
        sb.AppendLine($"{"Archivo",-24} {"Tipo",-20} {"SLOC",-8} {"Comentarios",-12} {"Total",-8} {"Procs",-6}");
        sb.AppendLine(new string('-', 82));

        foreach (var file in _summary.Files)
        {
            sb.AppendLine($"{file.FileName,-24} {file.FileType,-20} {file.CodeLines,-8} {file.CommentLines,-12} {file.TotalLines,-8} {file.ProcedureCount,-6}");
        }

        try
        {
            Clipboard.SetText(sb.ToString());
            MessageBox.Show(this, "Resumen de estadísticas copiado al portapapeles.", "Estadísticas de Código", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"No se pudo copiar al portapapeles: {ex.Message}", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
