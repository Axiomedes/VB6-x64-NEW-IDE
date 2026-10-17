using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using ViBa7.ProjectModel.Documents;
using ViBa7.ProjectModel.Vbp;

namespace ViBa7.IDE.Editor;

public record SearchResultItem(
    string FilePath,
    string FileName,
    int LineNumber,
    string LineText
);

public partial class FindInProjectDialog : Window
{
    private readonly IReadOnlyList<VB6Project> _projects;
    private readonly Action<string, int> _navigateTo;

    public FindInProjectDialog(VB6Project project, Action<string, int> navigateTo)
        : this([project], navigateTo)
    {
    }

    public FindInProjectDialog(IReadOnlyList<VB6Project> projects, Action<string, int> navigateTo)
    {
        InitializeComponent();
        _projects = projects;
        _navigateTo = navigateTo;

        Loaded += (_, _) =>
        {
            TxtSearchQuery.Focus();
            TxtSearchQuery.SelectAll();
        };
    }

    private void BtnSearch_Click(object sender, RoutedEventArgs e)
    {
        string query = TxtSearchQuery.Text;
        if (string.IsNullOrWhiteSpace(query)) return;

        LstResults.Items.Clear();
        TxtStatus.Text = "Buscando...";

        var results = new List<SearchResultItem>();
        string pattern = Regex.Escape(query);
        if (ChkWholeWord.IsChecked == true) pattern = $@"\b{pattern}\b";

        var options = RegexOptions.Multiline;
        if (ChkMatchCase.IsChecked != true) options |= RegexOptions.IgnoreCase;
        var regex = new Regex(pattern, options);

        int totalFilesSearched = 0;

        foreach (var project in _projects)
        {
            foreach (var item in project.Items)
            {
                if (!item.IsEditable || !item.FileExists || string.IsNullOrWhiteSpace(item.AbsolutePath)) continue;

                totalFilesSearched++;
                try
                {
                    var doc = SafeFileStorage.LoadDocument(item.AbsolutePath);
                    using var reader = new StringReader(doc.Text);
                    string? line;
                    int lineNum = 0;

                    while ((line = reader.ReadLine()) != null)
                    {
                        lineNum++;
                        if (regex.IsMatch(line))
                        {
                            results.Add(new SearchResultItem(
                                FilePath: item.AbsolutePath,
                                FileName: Path.GetFileName(item.AbsolutePath),
                                LineNumber: lineNum,
                                LineText: line.Trim()
                            ));
                        }
                    }
                }
                catch
                {
                    // Ignorar archivos que no se puedan leer
                }
            }
        }

        foreach (var r in results)
        {
            LstResults.Items.Add(r);
        }

        TxtStatus.Text = $"Se encontraron {results.Count} coincidencias en {totalFilesSearched} archivos.";
    }

    private void LstResults_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (LstResults.SelectedItem is SearchResultItem item)
        {
            _navigateTo(item.FilePath, item.LineNumber);
        }
    }
}
