using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using VB7x64.IDE.Designer;
using VB7x64.IDE.SyntaxHighlighting;
using VB7x64.ProjectModel.CodeBuilders;
using VB7x64.ProjectModel.Designers;
using VB7x64.ProjectModel.Documents;
using VB7x64.IDE.Logging;
using VB7x64.ProjectModel.Navigation;
using VB7x64.ProjectModel.Settings;
using VB7x64.IDE.Editor.IntelliSense;

namespace VB7x64.IDE.Editor;

public class EditorTabItem : TabItem, INotifyPropertyChanged
{
    public EditorDocument Document { get; }
    public TextEditor Editor { get; }
    public AppSettings Settings { get; private set; }
    public IntelliSenseController IntelliSenseController { get; }
    public CurrentLineHighlighter CurrentLineHighlighter { get; }
    public BranchLinesRenderer BranchLinesRenderer { get; }
    public EndOfLineDetailsRenderer EndOfLineDetailsRenderer { get; }
    public BookmarkMargin BookmarkMargin { get; }
    private readonly ComboBox _cmbProcedures;

    // Soporte para Diseñador Visual Dual (Código F7 / Diseñador Shift+F7)
    public FormDesignSurface? DesignSurface { get; private set; }
    public FormDesignModel? DesignModel { get; private set; }
    public bool IsDesignerActive { get; private set; }
    public event Action<bool>? ViewModeChanged;

    private Button? _btnToggleView;
    private Border _navPanel = null!;

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
        Settings = settings;

        Editor = new TextEditor
        {
            WordWrap = false,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(6, 0, 0, 0)
        };

        Editor.Options.ConvertTabsToSpaces = false;
        Editor.Options.EnableRectangularSelection = true;
        Editor.Options.EnableVirtualSpace = false;

        CurrentLineHighlighter = new CurrentLineHighlighter(Editor);
        BranchLinesRenderer = new BranchLinesRenderer(Editor);
        EndOfLineDetailsRenderer = new EndOfLineDetailsRenderer(Editor);
        BookmarkMargin = new BookmarkMargin(Editor, document.FilePath);

        Editor.TextArea.TextView.BackgroundRenderers.Add(CurrentLineHighlighter);
        Editor.TextArea.TextView.BackgroundRenderers.Add(BranchLinesRenderer);
        Editor.TextArea.TextView.BackgroundRenderers.Add(EndOfLineDetailsRenderer);
        EnsureLeftMargins();

        ApplyThemeSettings(settings);
        IntelliSenseController = new IntelliSenseController(this);

        Editor.Text = document.Text;

        InitializeHeader();
        SetupContextMenu();

        // Inicializar Diseñador Visual si el documento es un formulario o UserControl VB6
        if (document.HasHeader ||
            document.FilePath.EndsWith(".frm", StringComparison.OrdinalIgnoreCase) ||
            document.FilePath.EndsWith(".ctl", StringComparison.OrdinalIgnoreCase))
        {
            InitializeVisualDesigner();
        }

        // Barra superior de navegación de procedimientos
        _cmbProcedures = new ComboBox
        {
            MinWidth = 240,
            FontSize = 11.5,
            DisplayMemberPath = "DisplayText",
            ToolTip = "Saltar a procedimiento (Sub, Function, Property)"
        };
        _cmbProcedures.SelectionChanged += CmbProcedures_SelectionChanged;

        _navPanel = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(248, 248, 248)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(225, 225, 225)),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(4, 2, 4, 2),
            Height = 26
        };

        var dockPanel = new DockPanel();

        if (DesignSurface != null)
        {
            _btnToggleView = new Button
            {
                Content = "🗔 Diseñador (Shift+F7)",
                ToolTip = "Cambiar a la vista del Diseñador Visual",
                FontSize = 11,
                Padding = new Thickness(8, 1, 8, 1),
                Margin = new Thickness(2, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand
            };
            _btnToggleView.Click += (_, _) => SwitchToDesignerView();

            DockPanel.SetDock(_btnToggleView, Dock.Left);
            dockPanel.Children.Add(_btnToggleView);
        }

        _cmbProcedures.HorizontalAlignment = HorizontalAlignment.Right;
        _cmbProcedures.VerticalAlignment = VerticalAlignment.Center;
        _cmbProcedures.Height = 22;
        dockPanel.Children.Add(_cmbProcedures);
        _navPanel.Child = dockPanel;

        var contentGrid = new Grid();
        contentGrid.Children.Add(Editor);
        if (DesignSurface != null)
        {
            contentGrid.Children.Add(DesignSurface);
            Editor.Visibility = Visibility.Collapsed;
            _navPanel.Visibility = Visibility.Collapsed;
            DesignSurface.Visibility = Visibility.Visible;
            IsDesignerActive = true;
        }

        var rootGrid = new Grid();
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        Grid.SetRow(_navPanel, 0);
        Grid.SetRow(contentGrid, 1);
        rootGrid.Children.Add(_navPanel);
        rootGrid.Children.Add(contentGrid);

        Content = rootGrid;
        DataContext = this;

        // Escuchar cambios de edición
        Editor.TextChanged += Editor_TextChanged;
        Editor.TextArea.Caret.PositionChanged += Caret_PositionChanged;
        Editor.TextArea.PreviewKeyDown += TextArea_PreviewKeyDown;

        DataObject.AddCopyingHandler(Editor, (_, _) =>
        {
            string sel = Editor.SelectedText;
            if (!string.IsNullOrWhiteSpace(sel))
            {
                ClipboardRing.Instance.Add(sel);
            }
        });

        RefreshProcedures();
    }

    private void TextArea_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Tab && Keyboard.Modifiers == ModifierKeys.None)
        {
            if (Editor.SelectionLength == 0 && Editor.Document != null)
            {
                int caretOffset = Editor.CaretOffset;
                var line = Editor.Document.GetLineByOffset(caretOffset);
                string lineTextBeforeCaret = Editor.Document.GetText(line.Offset, caretOffset - line.Offset);

                int wordStart = lineTextBeforeCaret.Length;
                while (wordStart > 0 && char.IsLetterOrDigit(lineTextBeforeCaret[wordStart - 1]))
                {
                    wordStart--;
                }

                if (wordStart < lineTextBeforeCaret.Length)
                {
                    string shortcut = lineTextBeforeCaret[wordStart..];

                    int indentLen = 0;
                    while (indentLen < lineTextBeforeCaret.Length && (lineTextBeforeCaret[indentLen] == ' ' || lineTextBeforeCaret[indentLen] == '\t'))
                    {
                        indentLen++;
                    }
                    string indent = lineTextBeforeCaret[..indentLen];

                    if (SnippetManager.TryExpandSnippet(shortcut, indent, out string expandedText, out int relativeCaret))
                    {
                        int replaceStart = line.Offset + wordStart;
                        int replaceLen = shortcut.Length;
                        using (Editor.Document.RunUpdate())
                        {
                            Editor.Document.Replace(replaceStart, replaceLen, expandedText);
                        }
                        Editor.CaretOffset = replaceStart + relativeCaret;
                        e.Handled = true;
                    }
                }
            }
        }
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

    private int _lastNavLine = 1;

    private void Caret_PositionChanged(object? sender, EventArgs e)
    {
        int line = Editor.TextArea.Caret.Line;
        int col = Editor.TextArea.Caret.Column;
        CaretPositionChanged?.Invoke(line, col);

        if (Math.Abs(line - _lastNavLine) >= 10)
        {
            _lastNavLine = line;
            NavigationHistory.Instance.RecordNavigation(Document.FilePath, line, col);
        }
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
        Settings = settings;
        Editor.FontFamily = new FontFamily(settings.FontFamily);
        Editor.FontSize = settings.FontSize;
        Editor.ShowLineNumbers = settings.ShowLineNumbers;
        EnsureLeftMargins();
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

        CurrentLineHighlighter.IsEnabled = settings.EnableCurrentLineHighlight;
        try
        {
            if (ColorConverter.ConvertFromString(settings.CurrentLineHighlightColor) is Color hlColor)
            {
                CurrentLineHighlighter.UpdateBrush(hlColor);
            }
        }
        catch { }

        BranchLinesRenderer.IsEnabled = settings.EnableBranchLines;
        try
        {
            if (ColorConverter.ConvertFromString(settings.BranchLinesColor) is Color brColor)
            {
                BranchLinesRenderer.UpdateColor(brColor);
            }
        }
        catch { }

        EndOfLineDetailsRenderer.IsEnabled = settings.EnableEndOfLineDetails;
        EndOfLineDetailsRenderer.MinLines = settings.EndOfLineDetailsMinLines;
        try
        {
            if (ColorConverter.ConvertFromString(settings.EndOfLineDetailsColor) is Color eolColor)
            {
                EndOfLineDetailsRenderer.UpdateColor(eolColor);
            }
        }
        catch { }

        Editor.TextArea?.TextView?.Redraw();
    }

    private FrameworkElement? _spacerMargin;

    public void EnsureLeftMargins()
    {
        if (Editor.TextArea != null && Editor.TextArea.TextView != null)
        {
            Editor.TextArea.TextView.ClipToBounds = false;

            // Asegurar el margen de marcadores interactivo en el extremo izquierdo
            if (BookmarkMargin != null && !Editor.TextArea.LeftMargins.Contains(BookmarkMargin))
            {
                Editor.TextArea.LeftMargins.Insert(0, BookmarkMargin);
            }

            // Eliminar la línea separadora punteada/continua que AvalonEdit inserta por defecto (DottedLineMargin / Line)
            // para que no quede superpuesta con las líneas guía de bloques
            for (int i = Editor.TextArea.LeftMargins.Count - 1; i >= 0; i--)
            {
                var m = Editor.TextArea.LeftMargins[i];
                if (m is System.Windows.Shapes.Line || m.GetType().Name.Contains("DottedLine"))
                {
                    Editor.TextArea.LeftMargins.RemoveAt(i);
                }
            }

            bool hasLineNumbers = false;
            foreach (var margin in Editor.TextArea.LeftMargins)
            {
                if (margin is ICSharpCode.AvalonEdit.Editing.LineNumberMargin lnm)
                {
                    lnm.Margin = new Thickness(0, 0, 16, 0);
                    hasLineNumbers = true;
                }
            }

            if (!hasLineNumbers)
            {
                _spacerMargin ??= new Border { Width = 16 };
                if (!Editor.TextArea.LeftMargins.Contains(_spacerMargin))
                {
                    Editor.TextArea.LeftMargins.Add(_spacerMargin);
                }
            }
            else if (_spacerMargin != null && Editor.TextArea.LeftMargins.Contains(_spacerMargin))
            {
                Editor.TextArea.LeftMargins.Remove(_spacerMargin);
            }
        }
    }

    public string GetWordUnderCaret()
    {
        if (Editor.SelectionLength > 0)
        {
            string sel = Editor.SelectedText.Trim();
            if (System.Text.RegularExpressions.Regex.IsMatch(sel, @"^[a-zA-Z_][a-zA-Z0-9_]*$"))
            {
                return sel;
            }
        }

        if (Editor.Document == null) return string.Empty;
        int offset = Editor.CaretOffset;
        if (offset < 0 || offset > Editor.Document.TextLength) return string.Empty;

        var line = Editor.Document.GetLineByOffset(offset);
        string text = Editor.Document.GetText(line.Offset, line.Length);
        int colInLine = offset - line.Offset;

        if (colInLine < 0 || colInLine > text.Length) return string.Empty;

        int start = colInLine;
        while (start > 0 && (char.IsLetterOrDigit(text[start - 1]) || text[start - 1] == '_'))
        {
            start--;
        }

        int end = colInLine;
        while (end < text.Length && (char.IsLetterOrDigit(text[end]) || text[end] == '_'))
        {
            end++;
        }

        if (end > start)
        {
            string word = text[start..end];
            if (System.Text.RegularExpressions.Regex.IsMatch(word, @"^[a-zA-Z_][a-zA-Z0-9_]*$"))
            {
                return word;
            }
        }

        return string.Empty;
    }

    private void SetupContextMenu()
    {
        var menu = new ContextMenu();

        var miCut = new MenuItem { Header = "Cortar", InputGestureText = "Ctrl+X" };
        miCut.Click += (_, _) => ApplicationCommands.Cut.Execute(null, Editor);
        var miCopy = new MenuItem { Header = "Copiar", InputGestureText = "Ctrl+C" };
        miCopy.Click += (_, _) => ApplicationCommands.Copy.Execute(null, Editor);
        var miPaste = new MenuItem { Header = "Pegar", InputGestureText = "Ctrl+V" };
        miPaste.Click += (_, _) => ApplicationCommands.Paste.Execute(null, Editor);

        var miFindRefs = new MenuItem { Header = "Buscar todas las referencias...", InputGestureText = "Shift+F12" };
        miFindRefs.Click += (_, _) =>
        {
            if (Application.Current.MainWindow is MainWindow mw)
            {
                mw.FindReferencesForCurrentSymbol();
            }
        };

        var miToggleBm = new MenuItem { Header = "Alternar Marcador", InputGestureText = "Ctrl+F2" };
        miToggleBm.Click += (_, _) =>
        {
            int line = Editor.TextArea.Caret.Line;
            BookmarkManager.Instance.ToggleBookmark(Document.FilePath, line);
        };

        var miNextBm = new MenuItem { Header = "Siguiente Marcador", InputGestureText = "F2" };
        miNextBm.Click += (_, _) =>
        {
            if (Application.Current.MainWindow is MainWindow mw)
            {
                mw.GoToNextBookmark();
            }
        };

        var miPrevBm = new MenuItem { Header = "Marcador Anterior", InputGestureText = "Shift+F2" };
        miPrevBm.Click += (_, _) =>
        {
            if (Application.Current.MainWindow is MainWindow mw)
            {
                mw.GoToPreviousBookmark();
            }
        };

        var miComment = new MenuItem { Header = "Comentar Bloque", InputGestureText = "Ctrl+K, Ctrl+C" };
        miComment.Click += (_, _) =>
        {
            if (Application.Current.MainWindow is MainWindow mw)
            {
                mw.CommentSelection();
            }
        };

        var miUncomment = new MenuItem { Header = "Descomentar Bloque", InputGestureText = "Ctrl+K, Ctrl+U" };
        miUncomment.Click += (_, _) =>
        {
            if (Application.Current.MainWindow is MainWindow mw)
            {
                mw.UncommentSelection();
            }
        };

        menu.Items.Add(miCut);
        menu.Items.Add(miCopy);
        menu.Items.Add(miPaste);
        menu.Items.Add(new Separator());
        menu.Items.Add(miFindRefs);
        menu.Items.Add(new Separator());
        menu.Items.Add(miToggleBm);
        menu.Items.Add(miNextBm);
        menu.Items.Add(miPrevBm);
        menu.Items.Add(new Separator());
        menu.Items.Add(miComment);
        menu.Items.Add(miUncomment);

        Editor.ContextMenu = menu;
    }

    public void SetCurrentLineHighlightEnabled(bool enabled)
    {
        CurrentLineHighlighter.IsEnabled = enabled;
        Editor.TextArea?.TextView?.InvalidateLayer(ICSharpCode.AvalonEdit.Rendering.KnownLayer.Background);
    }

    public void SetBranchLinesEnabled(bool enabled)
    {
        BranchLinesRenderer.IsEnabled = enabled;
        Editor.TextArea?.TextView?.InvalidateLayer(ICSharpCode.AvalonEdit.Rendering.KnownLayer.Background);
    }

    public void SetEndOfLineDetailsEnabled(bool enabled)
    {
        EndOfLineDetailsRenderer.IsEnabled = enabled;
        Editor.TextArea?.TextView?.InvalidateLayer(ICSharpCode.AvalonEdit.Rendering.KnownLayer.Background);
    }

    #region Soporte para Diseñador Visual Dual (F7 / Shift+F7)
    private void InitializeVisualDesigner()
    {
        try
        {
            string headerText = Document.Header ?? string.Empty;
            if (string.IsNullOrWhiteSpace(headerText))
            {
                bool isCtl = Document.FilePath.EndsWith(".ctl", StringComparison.OrdinalIgnoreCase);
                if (isCtl)
                {
                    headerText = $"VERSION 5.00\r\nBegin VB.UserControl {TitleWithoutExtension}\r\n   ClientHeight = 3600\r\n   ClientLeft = 0\r\n   ClientTop = 0\r\n   ClientWidth = 4800\r\n   ScaleHeight = 3600\r\n   ScaleWidth = 4800\r\nEnd\r\n";
                }
                else
                {
                    headerText = $"VERSION 5.00\r\nBegin VB.Form {TitleWithoutExtension}\r\n   Caption = \"{TitleWithoutExtension}\"\r\n   ClientHeight = 3195\r\n   ClientLeft = 60\r\n   ClientTop = 345\r\n   ClientWidth = 4680\r\nEnd\r\n";
                }
            }

            DesignModel = FormDesignParser.Parse(headerText);
            DesignSurface = new FormDesignSurface();
            DesignSurface.LoadModel(DesignModel);

            DesignSurface.RequestCodeView += () => SwitchToCodeView();

            DesignSurface.ModelModified += () =>
            {
                if (DesignModel != null)
                {
                    Document.Header = FormDesignSerializer.Serialize(DesignModel);
                    Document.IsDirty = true;
                    OnPropertyChanged(nameof(HeaderText));
                }
            };

            DesignSurface.ControlDoubleClicked += ctrl =>
            {
                NavigateToControlEvent(ctrl);
            };
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"No se pudo inicializar el Diseñador Visual para {Document.DisplayName}: {ex.Message}");
        }
    }

    public void SwitchToCodeView()
    {
        if (DesignSurface != null && DesignModel != null)
        {
            Document.Header = FormDesignSerializer.Serialize(DesignModel);
            DesignSurface.Visibility = Visibility.Collapsed;
        }
        Editor.Visibility = Visibility.Visible;
        _navPanel.Visibility = Visibility.Visible;
        IsDesignerActive = false;
        ViewModeChanged?.Invoke(false);
        Editor.Focus();
    }

    public void SwitchToDesignerView()
    {
        if (DesignSurface == null) return;
        Editor.Visibility = Visibility.Collapsed;
        _navPanel.Visibility = Visibility.Collapsed;
        DesignSurface.Visibility = Visibility.Visible;
        IsDesignerActive = true;
        ViewModeChanged?.Invoke(true);
        DesignSurface.Focus();
    }

    private void NavigateToControlEvent(FormDesignerControl ctrl)
    {
        SwitchToCodeView();

        string eventName = GetDefaultEventName(ctrl.TypeName);
        string procName = $"{ctrl.Name}_{eventName}";
        string pattern = $"Sub {procName}";

        int targetLine = -1;
        var lines = Editor.Text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                targetLine = i + 1;
                break;
            }
        }

        if (targetLine != -1)
        {
            var lineObj = Editor.Document.GetLineByNumber(targetLine);
            Editor.CaretOffset = lineObj.Offset;
            Editor.ScrollToLine(targetLine);
        }
        else
        {
            string codeSnippet = $"\r\nPrivate Sub {procName}()\r\n    \r\nEnd Sub\r\n";
            int offset = Editor.Document.TextLength;
            Editor.Document.Insert(offset, codeSnippet);
            Editor.CaretOffset = offset + $"\r\nPrivate Sub {procName}()\r\n    ".Length;
            Editor.ScrollToLine(Editor.Document.LineCount);
        }
    }

    private static string GetDefaultEventName(string typeName)
    {
        if (typeName.EndsWith("CommandButton", StringComparison.OrdinalIgnoreCase)) return "Click";
        if (typeName.EndsWith("TextBox", StringComparison.OrdinalIgnoreCase)) return "Change";
        if (typeName.EndsWith("Timer", StringComparison.OrdinalIgnoreCase)) return "Timer";
        if (typeName.EndsWith("Form", StringComparison.OrdinalIgnoreCase)) return "Load";
        if (typeName.EndsWith("CheckBox", StringComparison.OrdinalIgnoreCase)) return "Click";
        if (typeName.EndsWith("OptionButton", StringComparison.OrdinalIgnoreCase)) return "Click";
        if (typeName.EndsWith("ListBox", StringComparison.OrdinalIgnoreCase)) return "Click";
        if (typeName.EndsWith("ComboBox", StringComparison.OrdinalIgnoreCase)) return "Click";
        return "Click";
    }
    #endregion

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
