using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using ICSharpCode.AvalonEdit;
using VB7x64.IDE.Editor;
using VB7x64.IDE.Logging;
using VB7x64.IDE.Settings;
using VB7x64.ProjectModel.Documents;
using VB7x64.ProjectModel.Settings;
using VB7x64.ProjectModel.Vbp;

namespace VB7x64.IDE;

public partial class MainWindow : Window
{
    private readonly AppSettings _settings;
    private VB6Project? _currentProject;
    private VB6ProjectGroup? _currentProjectGroup;

    public EditorTabItem? ActiveTab => DocumentsTabControl.SelectedItem as EditorTabItem;
    public TextEditor? ActiveEditor => ActiveTab?.Editor;

    public MainWindow()
    {
        InitializeComponent();
        _settings = AppSettings.Load();

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        DocumentsTabControl.SelectionChanged += DocumentsTabControl_SelectionChanged;

        // Suscribirse al stream de log en tiempo real
        AppLogger.LogMessageReceived += OnLogMessageReceived;

        UpdateDocumentsVisibility();
    }

    private void OnLogMessageReceived(string logEntry)
    {
        Dispatcher.Invoke(() =>
        {
            if (TxtOutputLog.Text.Length > 100000)
            {
                TxtOutputLog.Text = TxtOutputLog.Text[^50000..];
            }
            TxtOutputLog.AppendText(logEntry + Environment.NewLine);
            TxtOutputLog.ScrollToEnd();
        });
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Restaurar posición y tamaño de ventana
        if (_settings.WindowWidth > 200) Width = _settings.WindowWidth;
        if (_settings.WindowHeight > 200) Height = _settings.WindowHeight;
        if (_settings.WindowLeft >= 0) Left = _settings.WindowLeft;
        if (_settings.WindowTop >= 0) Top = _settings.WindowTop;
        if (_settings.IsMaximized) WindowState = WindowState.Maximized;

        // Restaurar estado de paneles
        MenuToggleProjectExplorer.IsChecked = _settings.ShowProjectExplorer;
        ColProjectExplorer.Width = _settings.ShowProjectExplorer ? new GridLength(_settings.ProjectExplorerWidth) : new GridLength(0);

        MenuToggleOutput.IsChecked = _settings.ShowOutputPanel;
        SetOutputPanelVisibility(_settings.ShowOutputPanel);

        // Restaurar estado de Funciones Avanzadas VB7
        MenuToggleLineHighlight.IsChecked = _settings.EnableCurrentLineHighlight;
        BtnToolbarToggleLineHighlight.IsChecked = _settings.EnableCurrentLineHighlight;
        MenuToggleBranchLines.IsChecked = _settings.EnableBranchLines;
        BtnToolbarToggleBranchLines.IsChecked = _settings.EnableBranchLines;

        PreviewKeyDown += MainWindow_PreviewKeyDown;

        UpdateRecentMenus();

        AppLogger.Info("Ventana principal cargada correctamente.");
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Verificar si hay documentos con cambios pendientes
        foreach (var item in DocumentsTabControl.Items)
        {
            if (item is EditorTabItem tab && tab.Document.IsDirty)
            {
                var response = MessageBox.Show(
                    $"¿Deseas guardar los cambios en '{tab.Document.DisplayName}' antes de salir?",
                    "Cambios pendientes",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (response == MessageBoxResult.Cancel)
                {
                    e.Cancel = true;
                    return;
                }

                if (response == MessageBoxResult.Yes)
                {
                    var result = tab.Save(_settings.CreateBackupBeforeSave);
                    if (result.Status != SaveStatus.Success && result.Status != SaveStatus.NotModified)
                    {
                        MessageBox.Show(result.Message, "Error al guardar", MessageBoxButton.OK, MessageBoxImage.Error);
                        e.Cancel = true;
                        return;
                    }
                }
            }
        }

        // Guardar configuración de ventana y paneles
        _settings.IsMaximized = WindowState == WindowState.Maximized;
        if (WindowState == WindowState.Normal)
        {
            _settings.WindowWidth = Width;
            _settings.WindowHeight = Height;
            _settings.WindowLeft = Left;
            _settings.WindowTop = Top;
        }

        _settings.ShowProjectExplorer = MenuToggleProjectExplorer.IsChecked;
        if (ColProjectExplorer.Width.Value > 50)
        {
            _settings.ProjectExplorerWidth = ColProjectExplorer.Width.Value;
        }

        _settings.ShowOutputPanel = MenuToggleOutput.IsChecked;
        if (RowOutput.Height.Value > 30)
        {
            _settings.OutputPanelHeight = RowOutput.Height.Value;
        }

        _settings.Save();
        AppLogger.Info("Configuración guardada.");
    }

    private void UpdateRecentMenus()
    {
        // Actualizar menú de proyectos recientes
        MenuRecentProjects.Items.Clear();
        if (_settings.RecentProjects.Count == 0)
        {
            MenuRecentProjects.Items.Add(new MenuItem { Header = "(Vacío)", IsEnabled = false });
        }
        else
        {
            foreach (var prj in _settings.RecentProjects)
            {
                var mi = new MenuItem { Header = Path.GetFileName(prj), ToolTip = prj };
                mi.Click += (_, _) => LoadProject(prj);
                MenuRecentProjects.Items.Add(mi);
            }
        }

        // Actualizar menú de archivos recientes
        MenuRecentFiles.Items.Clear();
        if (_settings.RecentFiles.Count == 0)
        {
            MenuRecentFiles.Items.Add(new MenuItem { Header = "(Vacío)", IsEnabled = false });
        }
        else
        {
            foreach (var file in _settings.RecentFiles)
            {
                var mi = new MenuItem { Header = Path.GetFileName(file), ToolTip = file };
                mi.Click += (_, _) => OpenFile(file);
                MenuRecentFiles.Items.Add(mi);
            }
        }
    }

    private void DocumentsTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateStatusFromActiveTab();
    }

    private void UpdateStatusFromActiveTab()
    {
        if (ActiveTab != null)
        {
            int line = ActiveTab.Editor.TextArea.Caret.Line;
            int col = ActiveTab.Editor.TextArea.Caret.Column;
            SetLineCol(line, col);

            string encName = ActiveTab.Document.Encoding.EncodingName;
            string bomInfo = ActiveTab.Document.HasByteOrderMark ? " (BOM)" : "";
            string nl = ActiveTab.Document.NewLineConvention.ToString().ToUpperInvariant();
            SetEncodingInfo($"{encName}{bomInfo}", nl);
            SetStatusMessage($"Documento activo: {ActiveTab.Document.DisplayName}");
        }
        else
        {
            SetLineCol(1, 1);
            SetEncodingInfo("Windows-1252 (ANSI)", "CRLF");
            SetStatusMessage("Listo");
        }
    }

    private void UpdateDocumentsVisibility()
    {
        if (DocumentsTabControl.Items.Count == 0)
        {
            EmptyDocsNotice.Visibility = Visibility.Visible;
            DocumentsTabControl.Visibility = Visibility.Collapsed;
        }
        else
        {
            EmptyDocsNotice.Visibility = Visibility.Collapsed;
            DocumentsTabControl.Visibility = Visibility.Visible;
        }
    }

    public void SetStatusMessage(string message) => TxtStatusMessage.Text = message;
    public void SetLineCol(int line, int col) => TxtStatusLineCol.Text = $"Ln {line}, Col {col}";
    public void SetEncodingInfo(string encoding, string lineEnding) => TxtStatusEncoding.Text = $"{encoding} | {lineEnding}";

    public EditorTabItem? OpenFile(string filePath)
    {
        try
        {
            string fullPath = Path.GetFullPath(filePath);

            // Evitar abrir duplicados para la misma ruta canónica
            foreach (var item in DocumentsTabControl.Items)
            {
                if (item is EditorTabItem openTab &&
                    string.Equals(Path.GetFullPath(openTab.Document.FilePath), fullPath, StringComparison.OrdinalIgnoreCase))
                {
                    DocumentsTabControl.SelectedItem = openTab;
                    UpdateStatusFromActiveTab();
                    return openTab;
                }
            }

            var doc = SafeFileStorage.LoadDocument(fullPath);
            var tabItem = new EditorTabItem(doc, _settings);

            tabItem.CaretPositionChanged += (line, col) =>
            {
                if (DocumentsTabControl.SelectedItem == tabItem)
                {
                    SetLineCol(line, col);
                }
            };

            DocumentsTabControl.Items.Add(tabItem);
            DocumentsTabControl.SelectedItem = tabItem;

            _settings.AddRecentFile(fullPath);
            UpdateRecentMenus();

            UpdateDocumentsVisibility();
            UpdateStatusFromActiveTab();
            AppLogger.Info($"Archivo abierto: {fullPath}");
            return tabItem;
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Error al abrir el archivo {filePath}", ex);
            MessageBox.Show($"No se pudo abrir el archivo:\n\n{ex.Message}", "Error al abrir archivo", MessageBoxButton.OK, MessageBoxImage.Error);
            return null;
        }
    }

    public bool CloseTab(EditorTabItem tab)
    {
        if (tab.Document.IsDirty)
        {
            var response = MessageBox.Show(
                $"¿Deseas guardar los cambios en '{tab.Document.DisplayName}' antes de cerrar?",
                "Cambios pendientes",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (response == MessageBoxResult.Cancel) return false;

            if (response == MessageBoxResult.Yes)
            {
                var result = tab.Save(_settings.CreateBackupBeforeSave);
                if (result.Status != SaveStatus.Success && result.Status != SaveStatus.NotModified)
                {
                    MessageBox.Show(result.Message, "Error al guardar", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
            }
        }

        DocumentsTabControl.Items.Remove(tab);
        UpdateDocumentsVisibility();
        UpdateStatusFromActiveTab();
        return true;
    }

    #region Project Handling (Milestone 5 & VBG Groups)
    public void LoadProject(string filePath)
    {
        string ext = Path.GetExtension(filePath);
        if (string.Equals(ext, ".vbg", StringComparison.OrdinalIgnoreCase))
        {
            LoadProjectGroup(filePath);
        }
        else
        {
            LoadSingleProject(filePath);
        }
    }

    public void LoadProjectGroup(string vbgFilePath)
    {
        try
        {
            var group = VbgParser.Parse(vbgFilePath);
            _currentProjectGroup = group;
            _currentProject = group.StartupProject ?? group.Projects.FirstOrDefault();

            ProjectTreeView.Items.Clear();

            var groupRoot = new TreeViewItem
            {
                Header = $"💼 Grupo de Proyectos: {group.Name} ({Path.GetFileName(group.GroupFilePath)})",
                IsExpanded = true,
                FontWeight = FontWeights.Bold
            };

            foreach (var project in group.Projects)
            {
                bool isStartup = project == group.StartupProject;
                var prjNode = CreateProjectNode(project, isStartup);
                groupRoot.Items.Add(prjNode);
            }

            ProjectTreeView.Items.Add(groupRoot);
            SetStatusMessage($"Grupo de proyectos '{group.Name}' cargado con {group.Projects.Count} proyectos.");
            Title = $"VB7-x64 IDE - [Grupo] {group.Name} ({Path.GetFileName(group.GroupFilePath)})";

            _settings.AddRecentProject(group.GroupFilePath);
            UpdateRecentMenus();

            AppLogger.Info($"Grupo de proyectos cargado: {group.GroupFilePath}");
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Error al abrir el grupo de proyectos {vbgFilePath}", ex);
            MessageBox.Show($"Error al abrir el grupo de proyectos:\n\n{ex.Message}", "Error al abrir grupo de proyectos", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public void LoadSingleProject(string vbpFilePath)
    {
        try
        {
            var project = VbpParser.Parse(vbpFilePath);
            _currentProject = project;
            _currentProjectGroup = null;

            ProjectTreeView.Items.Clear();
            var prjNode = CreateProjectNode(project, false);
            ProjectTreeView.Items.Add(prjNode);

            SetStatusMessage($"Proyecto '{project.Name}' cargado exitosamente.");
            Title = $"VB7-x64 IDE - {project.Name} ({Path.GetFileName(project.ProjectFilePath)})";

            _settings.AddRecentProject(project.ProjectFilePath);
            UpdateRecentMenus();

            AppLogger.Info($"Proyecto cargado: {project.ProjectFilePath}");
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Error al abrir el proyecto {vbpFilePath}", ex);
            MessageBox.Show($"Error al abrir el proyecto:\n\n{ex.Message}", "Error al abrir proyecto", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static TreeViewItem CreateProjectNode(VB6Project project, bool isStartup)
    {
        string title = isStartup
            ? $"📁 {project.Name} ({Path.GetFileName(project.ProjectFilePath)}) [Inicio]"
            : $"📁 {project.Name} ({Path.GetFileName(project.ProjectFilePath)})";

        var rootItem = new TreeViewItem
        {
            Header = title,
            IsExpanded = true,
            FontWeight = isStartup ? FontWeights.Bold : FontWeights.SemiBold
        };

        // Formularios
        if (project.Forms.Any())
        {
            var formsFolder = new TreeViewItem { Header = $"🗔 Formularios ({project.Forms.Count()})", IsExpanded = true, FontWeight = FontWeights.SemiBold };
            foreach (var form in project.Forms)
            {
                formsFolder.Items.Add(CreateProjectItemNode(form));
            }
            rootItem.Items.Add(formsFolder);
        }

        // Módulos
        if (project.Modules.Any())
        {
            var modulesFolder = new TreeViewItem { Header = $"📜 Módulos ({project.Modules.Count()})", IsExpanded = true, FontWeight = FontWeights.SemiBold };
            foreach (var mod in project.Modules)
            {
                modulesFolder.Items.Add(CreateProjectItemNode(mod));
            }
            rootItem.Items.Add(modulesFolder);
        }

        // Clases
        if (project.Classes.Any())
        {
            var classesFolder = new TreeViewItem { Header = $"🧩 Módulos de Clase ({project.Classes.Count()})", IsExpanded = true, FontWeight = FontWeights.SemiBold };
            foreach (var cls in project.Classes)
            {
                classesFolder.Items.Add(CreateProjectItemNode(cls));
            }
            rootItem.Items.Add(classesFolder);
        }

        // Controles de Usuario
        if (project.UserControls.Any())
        {
            var ucFolder = new TreeViewItem { Header = $"🎛️ Controles de Usuario ({project.UserControls.Count()})", IsExpanded = true, FontWeight = FontWeights.SemiBold };
            foreach (var uc in project.UserControls)
            {
                ucFolder.Items.Add(CreateProjectItemNode(uc));
            }
            rootItem.Items.Add(ucFolder);
        }

        // Referencias
        if (project.References.Any())
        {
            var refFolder = new TreeViewItem { Header = $"🔗 Referencias ({project.References.Count})", IsExpanded = false, FontWeight = FontWeights.Normal };
            foreach (var r in project.References)
            {
                var refNode = new TreeViewItem
                {
                    Header = $"• {r.DisplayTitle}",
                    Foreground = Brushes.Gray,
                    FontWeight = FontWeights.Normal
                };
                refFolder.Items.Add(refNode);
            }
            rootItem.Items.Add(refFolder);
        }

        return rootItem;
    }

    private static TreeViewItem CreateProjectItemNode(ProjectItem item)
    {
        var node = new TreeViewItem
        {
            Header = $"{item.DisplayIcon} {item.DisplayTitle}" + (!item.FileExists ? " (No encontrado)" : ""),
            Tag = item,
            FontWeight = FontWeights.Normal
        };

        if (!item.FileExists)
        {
            node.Foreground = Brushes.Crimson;
            node.ToolTip = $"El archivo no fue encontrado en: {item.AbsolutePath}";
        }
        else
        {
            node.ToolTip = item.AbsolutePath;
        }

        return node;
    }

    private void ProjectTreeView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ProjectTreeView.SelectedItem is TreeViewItem { Tag: ProjectItem item })
        {
            if (!item.FileExists)
            {
                MessageBox.Show($"El archivo '{item.DisplayTitle}' no existe en la ruta esperada:\n\n{item.AbsolutePath}",
                    "Archivo no encontrado", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (item.IsEditable)
            {
                OpenFile(item.AbsolutePath);
            }
        }
    }
    #endregion

    #region Menu File Actions
    private void MenuOpenProject_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Abrir Proyecto o Grupo de Proyectos VB6",
            Filter = "Proyectos y Grupos VB (*.vbp;*.vbg)|*.vbp;*.vbg|Grupos de Proyectos (*.vbg)|*.vbg|Proyectos VB (*.vbp)|*.vbp|Todos los archivos (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            LoadProject(dialog.FileName);
        }
    }

    private void MenuOpenFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Abrir Archivo de Código VB6",
            Filter = "Archivos de Código VB6 (*.bas;*.cls;*.frm;*.ctl;*.pag;*.dob;*.dsr)|*.bas;*.cls;*.frm;*.ctl;*.pag;*.dob;*.dsr|Módulos (*.bas)|*.bas|Clases (*.cls)|*.cls|Formularios (*.frm)|*.frm|User Controls (*.ctl)|*.ctl|Páginas de Propiedades (*.pag)|*.pag|Documentos de Usuario (*.dob)|*.dob|Diseñadores (*.dsr)|*.dsr|Todos los archivos (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            OpenFile(dialog.FileName);
        }
    }

    private void MenuSave_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveTab == null) return;

        var result = ActiveTab.Save(_settings.CreateBackupBeforeSave);
        switch (result.Status)
        {
            case SaveStatus.Success:
                SetStatusMessage($"'{ActiveTab.Document.DisplayName}' guardado exitosamente.");
                break;
            case SaveStatus.NotModified:
                SetStatusMessage($"'{ActiveTab.Document.DisplayName}' no tiene cambios.");
                break;
            case SaveStatus.ReadOnlyFile:
            case SaveStatus.ExternalConflict:
            case SaveStatus.Error:
                MessageBox.Show(result.Message, "Error al guardar", MessageBoxButton.OK, MessageBoxImage.Warning);
                SetStatusMessage(result.Message);
                break;
        }
    }

    private void MenuSaveAs_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveTab == null) return;

        var dialog = new SaveFileDialog
        {
            Title = "Guardar como",
            FileName = ActiveTab.Document.DisplayName,
            Filter = "Archivos de Código VB6 (*.bas;*.cls;*.frm)|*.bas;*.cls;*.frm|Todos los archivos (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            ActiveTab.Document.FilePath = dialog.FileName;
            ActiveTab.Document.DisplayName = Path.GetFileName(dialog.FileName);
            ActiveTab.Document.IsDirty = true;
            MenuSave_Click(sender, e);
        }
    }

    private void MenuSaveAll_Click(object sender, RoutedEventArgs e)
    {
        int count = 0;
        foreach (var item in DocumentsTabControl.Items)
        {
            if (item is EditorTabItem tab && tab.Document.IsDirty)
            {
                var result = tab.Save(_settings.CreateBackupBeforeSave);
                if (result.Status == SaveStatus.Success) count++;
            }
        }
        SetStatusMessage(count > 0 ? $"Se guardaron {count} documentos modificados." : "Todos los documentos están actualizados.");
    }

    private void MenuCloseDoc_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveTab != null)
        {
            CloseTab(ActiveTab);
        }
    }

    private void MenuExit_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
    #endregion

    #region Menu Edit Actions
    private void MenuUndo_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveEditor != null && ActiveEditor.CanUndo) ActiveEditor.Undo();
    }

    private void MenuRedo_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveEditor != null && ActiveEditor.CanRedo) ActiveEditor.Redo();
    }

    private void MenuCut_Click(object sender, RoutedEventArgs e)
    {
        ActiveEditor?.Cut();
    }

    private void MenuCopy_Click(object sender, RoutedEventArgs e)
    {
        ActiveEditor?.Copy();
    }

    private void MenuPaste_Click(object sender, RoutedEventArgs e)
    {
        ActiveEditor?.Paste();
    }

    private void MenuFind_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveEditor == null) return;
        var dlg = new FindReplaceDialog(ActiveEditor) { Owner = this };
        dlg.Show();
    }

    private void MenuReplace_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveEditor == null) return;
        var dlg = new FindReplaceDialog(ActiveEditor) { Owner = this };
        dlg.Show();
    }

    private void MenuFindInProject_Click(object sender, RoutedEventArgs e)
    {
        IReadOnlyList<VB6Project> projectsToSearch;
        if (_currentProjectGroup != null && _currentProjectGroup.Projects.Count > 0)
        {
            projectsToSearch = _currentProjectGroup.Projects;
        }
        else if (_currentProject != null)
        {
            projectsToSearch = [_currentProject];
        }
        else
        {
            MessageBox.Show("Debes abrir un proyecto .vbp o grupo .vbg para buscar.", "Buscar en Proyecto", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dlg = new FindInProjectDialog(projectsToSearch, (filePath, line) =>
        {
            var tab = OpenFile(filePath);
            if (tab != null)
            {
                var lineObj = tab.Editor.Document.GetLineByNumber(Math.Min(line, tab.Editor.Document.LineCount));
                tab.Editor.CaretOffset = lineObj.Offset;
                tab.Editor.ScrollToLine(line);
                tab.Editor.Focus();
            }
        })
        {
            Owner = this
        };
        dlg.Show();
    }

    private void MenuGoToLine_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveEditor == null) return;

        int currentLine = ActiveEditor.TextArea.Caret.Line;
        int lineCount = ActiveEditor.Document.LineCount;

        var dlg = new GoToLineDialog(currentLine, lineCount) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            int targetLine = dlg.SelectedLine;
            var line = ActiveEditor.Document.GetLineByNumber(targetLine);
            ActiveEditor.CaretOffset = line.Offset;
            ActiveEditor.ScrollToLine(targetLine);
        }
    }
    #endregion

    #region Menu View Actions
    private void MenuToggleProjectExplorer_Click(object sender, RoutedEventArgs e)
    {
        ColProjectExplorer.Width = MenuToggleProjectExplorer.IsChecked ? new GridLength(_settings.ProjectExplorerWidth) : new GridLength(0);
    }

    private void MenuToggleStatusBar_Click(object sender, RoutedEventArgs e)
    {
        MainStatusBar.Visibility = MenuToggleStatusBar.IsChecked ? Visibility.Visible : Visibility.Collapsed;
    }

    private void MenuToggleOutput_Click(object sender, RoutedEventArgs e)
    {
        SetOutputPanelVisibility(MenuToggleOutput.IsChecked);
    }

    private void SetOutputPanelVisibility(bool isVisible)
    {
        RowOutput.Height = isVisible ? new GridLength(_settings.OutputPanelHeight) : new GridLength(0);
        RowOutputSplitter.Height = isVisible ? new GridLength(4) : new GridLength(0);
    }

    private void BtnClearOutput_Click(object sender, RoutedEventArgs e)
    {
        TxtOutputLog.Clear();
    }

    private void BtnCloseOutput_Click(object sender, RoutedEventArgs e)
    {
        MenuToggleOutput.IsChecked = false;
        SetOutputPanelVisibility(false);
    }

    private void MenuOpenLogsFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string dir = Path.GetDirectoryName(AppLogger.LogFilePath)!;
            Process.Start(new ProcessStartInfo
            {
                FileName = dir,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            AppLogger.Error("No se pudo abrir la carpeta de logs", ex);
        }
    }
    #endregion

    #region Tools & Options
    private void MenuOptions_Click(object sender, RoutedEventArgs e)
    {
        var opt = new OptionsWindow(_settings) { Owner = this };
        if (opt.ShowDialog() == true)
        {
            // Aplicar cambios a las pestañas abiertas
            foreach (var item in DocumentsTabControl.Items)
            {
                if (item is EditorTabItem tab)
                {
                    tab.ApplyThemeSettings(_settings);
                }
            }
        }
    }
    #endregion

    #region Menu Help Actions
    private void MenuAbout_Click(object sender, RoutedEventArgs e)
    {
        var about = new AboutWindow { Owner = this };
        about.ShowDialog();
    }
    #endregion

    #region Funciones Avanzadas VB7
    private bool _isChordCtrlK;

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // 1. Invertir Asignaciones: Ctrl+Shift+R
        if (e.Key == Key.R && (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            if (ActiveEditor != null)
            {
                EditorOperationsHelper.SwitchAssignment(ActiveEditor);
                SetStatusMessage("Asignaciones invertidas (A = B ⇄ B = A).");
                e.Handled = true;
                return;
            }
        }

        // 2. Secuencia acorde Ctrl+K, Ctrl+C / Ctrl+K, Ctrl+U
        if (_isChordCtrlK)
        {
            _isChordCtrlK = false;
            if (e.Key == Key.C)
            {
                if (ActiveEditor != null)
                {
                    EditorOperationsHelper.CommentSelection(ActiveEditor);
                    SetStatusMessage("Líneas comentadas.");
                }
                e.Handled = true;
                return;
            }
            if (e.Key == Key.U)
            {
                if (ActiveEditor != null)
                {
                    EditorOperationsHelper.UncommentSelection(ActiveEditor);
                    SetStatusMessage("Líneas descomentadas.");
                }
                e.Handled = true;
                return;
            }
            SetStatusMessage("Combinación Ctrl+K cancelada.");
        }
        else if (e.Key == Key.K && Keyboard.Modifiers == ModifierKeys.Control)
        {
            _isChordCtrlK = true;
            SetStatusMessage("Ctrl+K presionado. Esperando segunda tecla (C para comentar, U para descomentar)...");
            e.Handled = true;
            return;
        }
    }

    private void MenuToggleLineHighlight_Click(object sender, RoutedEventArgs e)
    {
        SetLineHighlightState(MenuToggleLineHighlight.IsChecked);
    }

    private void BtnToolbarToggleLineHighlight_Click(object sender, RoutedEventArgs e)
    {
        SetLineHighlightState(BtnToolbarToggleLineHighlight.IsChecked == true);
    }

    private void SetLineHighlightState(bool enabled)
    {
        _settings.EnableCurrentLineHighlight = enabled;
        MenuToggleLineHighlight.IsChecked = enabled;
        BtnToolbarToggleLineHighlight.IsChecked = enabled;

        foreach (var item in DocumentsTabControl.Items)
        {
            if (item is EditorTabItem tab)
            {
                tab.SetCurrentLineHighlightEnabled(enabled);
            }
        }

        SetStatusMessage(enabled ? "Resaltado de línea activa activado." : "Resaltado de línea activa desactivado.");
    }

    private void MenuToggleBranchLines_Click(object sender, RoutedEventArgs e)
    {
        SetBranchLinesState(MenuToggleBranchLines.IsChecked);
    }

    private void BtnToolbarToggleBranchLines_Click(object sender, RoutedEventArgs e)
    {
        SetBranchLinesState(BtnToolbarToggleBranchLines.IsChecked == true);
    }

    private void SetBranchLinesState(bool enabled)
    {
        _settings.EnableBranchLines = enabled;
        MenuToggleBranchLines.IsChecked = enabled;
        BtnToolbarToggleBranchLines.IsChecked = enabled;

        foreach (var item in DocumentsTabControl.Items)
        {
            if (item is EditorTabItem tab)
            {
                tab.SetBranchLinesEnabled(enabled);
            }
        }

        SetStatusMessage(enabled ? "Guías de bloques (Branch Lines) activadas." : "Guías de bloques desactivadas.");
    }

    private void MenuCommentBlock_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveEditor != null)
        {
            EditorOperationsHelper.CommentSelection(ActiveEditor);
            SetStatusMessage("Bloque comentado exitosamente.");
        }
    }

    private void MenuUncommentBlock_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveEditor != null)
        {
            EditorOperationsHelper.UncommentSelection(ActiveEditor);
            SetStatusMessage("Bloque descomentado exitosamente.");
        }
    }

    private void MenuSwitchAssignment_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveEditor != null)
        {
            EditorOperationsHelper.SwitchAssignment(ActiveEditor);
            SetStatusMessage("Asignaciones invertidas exitosamente (A = B ⇄ B = A).");
        }
    }

    private void MenuCaseUpper_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveEditor != null)
        {
            EditorOperationsHelper.ConvertCase(ActiveEditor, ProjectModel.Advanced.TextCaseMode.Upper);
            SetStatusMessage("Texto convertido a MAYÚSCULAS.");
        }
    }

    private void MenuCaseLower_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveEditor != null)
        {
            EditorOperationsHelper.ConvertCase(ActiveEditor, ProjectModel.Advanced.TextCaseMode.Lower);
            SetStatusMessage("Texto convertido a minúsculas.");
        }
    }

    private void MenuCaseTitle_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveEditor != null)
        {
            EditorOperationsHelper.ConvertCase(ActiveEditor, ProjectModel.Advanced.TextCaseMode.Title);
            SetStatusMessage("Texto convertido a Tipo Título.");
        }
    }
    #endregion

    private void BtnCloseTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is EditorTabItem tabItem)
        {
            CloseTab(tabItem);
        }
    }
}