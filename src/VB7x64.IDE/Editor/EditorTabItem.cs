using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using VB7x64.IDE.SyntaxHighlighting;
using VB7x64.ProjectModel.Documents;
using VB7x64.ProjectModel.Navigation;
using VB7x64.ProjectModel.Settings;

namespace VB7x64.IDE.Editor;

public class EditorTabItem : TabItem, INotifyPropertyChanged
{
    public EditorDocument Document { get; }
    public TextEditor Editor { get; }
    private readonly ComboBox _cmbProcedures;

    public event Action<int, int>? CaretPositionChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    public string TitleWithoutExtension => System.IO.Path.GetFileNameWithoutExtension(Document.FilePath);
    public string HeaderText => Document.IsDirty ? $"{TitleWithoutExtension} *" : TitleWithoutExtension;
    public string ToolTipText => $"{Document.DisplayName}\n{Document.FilePath}";
    public string FileExtension => System.IO.Path.GetExtension(Document.FilePath).ToLowerInvariant();
    public string IconText => FileExtension switch
    {
        ".frm" => "🗔",
        ".cls" => "🧩",
        ".bas" => "📜",
        ".ctl" => "🎛️",
        ".pag" => "📑",
        ".dob" => "📄",
        ".dsr" => "📊",
        _ => "📄"
    };

    private TextBlock _lblIcon = null!;
    private TextBlock _lblTitle = null!;
    private Button _btnClose = null!;

    public EditorTabItem(EditorDocument document, AppSettings settings)
    {
        Document = document;

        Editor = new TextEditor
        {
            WordWrap = false,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        Editor.Options.ConvertTabsToSpaces = false;
        Editor.Options.EnableRectangularSelection = true;
        Editor.Options.EnableVirtualSpace = false;

        ApplyThemeSettings(settings);

        Editor.Text = document.Text;

        InitializeHeader();

        // Barra superior de navegación de procedimientos
        _cmbProcedures = new ComboBox
        {
            MinWidth = 260,
            FontSize = 11.5,
            DisplayMemberPath = "DisplayText",
            ToolTip = "Saltar a procedimiento (Sub, Function, Property)"
        };
        _cmbProcedures.SelectionChanged += CmbProcedures_SelectionChanged;

        var navPanel = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(248, 248, 248)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(225, 225, 225)),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(6, 3, 6, 3)
        };

        var dockPanel = new DockPanel();
        var lblDoc = new TextBlock
        {
            Text = $"📄 {document.DisplayName}",
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 11.5,
            Foreground = new SolidColorBrush(Color.FromRgb(80, 80, 80))
        };
        DockPanel.SetDock(lblDoc, Dock.Left);
        dockPanel.Children.Add(lblDoc);

        _cmbProcedures.HorizontalAlignment = HorizontalAlignment.Right;
        dockPanel.Children.Add(_cmbProcedures);
        navPanel.Child = dockPanel;

        var rootGrid = new Grid();
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        Grid.SetRow(navPanel, 0);
        Grid.SetRow(Editor, 1);
        rootGrid.Children.Add(navPanel);
        rootGrid.Children.Add(Editor);

        Content = rootGrid;
        DataContext = this;

        // Escuchar cambios de edición
        Editor.TextChanged += Editor_TextChanged;
        Editor.TextArea.Caret.PositionChanged += Caret_PositionChanged;

        RefreshProcedures();
    }

    private void RefreshProcedures()
    {
        var procedures = ProcedureParser.ParseProcedures(Editor.Text);
        _cmbProcedures.ItemsSource = procedures;
    }

    private void CmbProcedures_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_cmbProcedures.SelectedItem is ProcedureSymbol sym && sym.LineNumber > 0)
        {
            if (sym.LineNumber <= Editor.Document.LineCount)
            {
                var line = Editor.Document.GetLineByNumber(sym.LineNumber);
                Editor.CaretOffset = line.Offset;
                Editor.ScrollToLine(sym.LineNumber);
                Editor.Focus();
            }
        }
    }

    private void InitializeHeader()
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };

        _lblIcon = new TextBlock
        {
            Text = IconText,
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0)
        };

        _lblTitle = new TextBlock
        {
            Text = HeaderText,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };

        _btnClose = new Button
        {
            Content = "✕",
            Width = 18,
            Height = 18,
            FontSize = 10,
            Padding = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand,
            ToolTip = "Cerrar documento",
            Tag = this
        };

        var btnStyle = new Style(typeof(Button));
        btnStyle.Setters.Add(new Setter(Button.ForegroundProperty, new SolidColorBrush(Color.FromRgb(120, 120, 120))));
        var hoverTrigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(Button.ForegroundProperty, new SolidColorBrush(Color.FromRgb(211, 47, 47))));
        hoverTrigger.Setters.Add(new Setter(Button.BackgroundProperty, new SolidColorBrush(Color.FromArgb(40, 255, 0, 0))));
        btnStyle.Triggers.Add(hoverTrigger);
        _btnClose.Style = btnStyle;

        _btnClose.Click += (_, e) =>
        {
            e.Handled = true;
            if (Application.Current.MainWindow is MainWindow mw)
            {
                mw.CloseTab(this);
            }
        };

        panel.Children.Add(_lblIcon);
        panel.Children.Add(_lblTitle);
        panel.Children.Add(_btnClose);

        Header = panel;
        ToolTip = ToolTipText;
    }

    public void UpdateHeaderDisplay()
    {
        if (_lblTitle != null)
        {
            _lblTitle.Text = HeaderText;
        }
        if (_lblIcon != null)
        {
            _lblIcon.Text = IconText;
        }
        ToolTip = ToolTipText;
        OnPropertyChanged(nameof(HeaderText));
        OnPropertyChanged(nameof(TitleWithoutExtension));
        OnPropertyChanged(nameof(ToolTipText));
    }

    private void Editor_TextChanged(object? sender, EventArgs e)
    {
        if (Document.Text != Editor.Text)
        {
            Document.Text = Editor.Text;
            UpdateHeaderDisplay();
            RefreshProcedures();
        }
    }

    private void Caret_PositionChanged(object? sender, EventArgs e)
    {
        int line = Editor.TextArea.Caret.Line;
        int col = Editor.TextArea.Caret.Column;
        CaretPositionChanged?.Invoke(line, col);
    }

    public SaveResult Save(bool createBackup)
    {
        Document.Text = Editor.Text;
        var result = SafeFileStorage.SaveDocument(Document, createBackup);
        if (result.Status == SaveStatus.Success || result.Status == SaveStatus.NotModified)
        {
            UpdateHeaderDisplay();
        }
        return result;
    }

    public void ApplyThemeSettings(AppSettings settings)
    {
        Editor.FontFamily = new FontFamily(settings.FontFamily);
        Editor.FontSize = settings.FontSize;
        Editor.ShowLineNumbers = settings.ShowLineNumbers;
        Editor.Options.IndentationSize = settings.TabSize;

        try
        {
            if (ColorConverter.ConvertFromString(settings.EditorBackgroundColor) is Color bg)
            {
                Editor.Background = new SolidColorBrush(bg);
            }
        }
        catch { Editor.Background = Brushes.White; }

        try
        {
            if (ColorConverter.ConvertFromString(settings.EditorForegroundColor) is Color fg)
            {
                Editor.Foreground = new SolidColorBrush(fg);
            }
        }
        catch { Editor.Foreground = Brushes.Black; }

        try
        {
            if (ColorConverter.ConvertFromString(settings.LineNumbersColor) is Color ln)
            {
                Editor.LineNumbersForeground = new SolidColorBrush(ln);
            }
        }
        catch { }

        Editor.SyntaxHighlighting = VB6SyntaxHighlighting.CreateDefinition(settings);
        Editor.TextArea?.TextView?.Redraw();
    }

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
