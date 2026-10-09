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
using VB7x64.IDE.CodeBuilders;
using VB7x64.IDE.Editor;
using VB7x64.IDE.Logging;
using VB7x64.IDE.Settings;
using VB7x64.ProjectModel.CodeBuilders;
using VB7x64.ProjectModel.Documents;
using VB7x64.ProjectModel.Navigation;
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
        MenuToggleEndOfLineDetails.IsChecked = _settings.EnableEndOfLineDetails;
        BtnToolbarToggleEndOfLineDetails.IsChecked = _settings.EnableEndOfLineDetails;

        UpdateExplorerModeUI();

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

            RefreshProjectTree();
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

            RefreshProjectTree();
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

    public void RefreshProjectTree()
    {
        string filter = TxtProjectSearch?.Text.Trim() ?? "";

        if (_currentProjectGroup != null)
        {
            ProjectTreeView.Items.Clear();
            var groupRoot = new TreeViewItem
            {
                Header = $"💼 Grupo de Proyectos: {_currentProjectGroup.Name} ({Path.GetFileName(_currentProjectGroup.GroupFilePath)})",
                IsExpanded = true,
                FontWeight = FontWeights.Bold
            };

            foreach (var project in _currentProjectGroup.Projects)
            {
                bool isStartup = project == _currentProjectGroup.StartupProject;
                var prjNode = CreateProjectNode(project, isStartup, filter, _settings.ProjectExplorerMode);
                if (prjNode != null)
                {
                    groupRoot.Items.Add(prjNode);
                }
            }

            if (groupRoot.Items.Count > 0)
            {
                ProjectTreeView.Items.Add(groupRoot);
            }
            else
            {
                ProjectTreeView.Items.Add(new TreeViewItem
                {
                    Header = "No se encontraron coincidencias para la búsqueda",
                    Foreground = Brushes.Gray,
                    FontStyle = FontStyles.Italic
                });
            }
        }
        else if (_currentProject != null)
        {
            ProjectTreeView.Items.Clear();
            var prjNode = CreateProjectNode(_currentProject, false, filter, _settings.ProjectExplorerMode);
            if (prjNode != null)
            {
                ProjectTreeView.Items.Add(prjNode);
            }
            else
            {
                ProjectTreeView.Items.Add(new TreeViewItem
                {
                    Header = "No se encontraron coincidencias para la búsqueda",
                    Foreground = Brushes.Gray,
                    FontStyle = FontStyles.Italic
                });
            }
        }
        else
        {
            ProjectTreeView.Items.Clear();
            ProjectTreeView.Items.Add(new TreeViewItem
            {
                Header = "Ningún proyecto abierto",
                IsSelected = true,
                Foreground = Brushes.Gray,
                FontStyle = FontStyles.Italic
            });
        }
    }

    private TreeViewItem? CreateProjectNode(VB6Project project, bool isStartup, string filter, ProjectExplorerMode mode)
    {
        bool hasFilter = !string.IsNullOrEmpty(filter);
        string title = isStartup
            ? $"📁 {project.Name} ({Path.GetFileName(project.ProjectFilePath)}) [Inicio]"
            : $"📁 {project.Name} ({Path.GetFileName(project.ProjectFilePath)})";

        var rootItem = new TreeViewItem
        {
            Header = title,
            IsExpanded = true,
            FontWeight = isStartup ? FontWeights.Bold : FontWeights.SemiBold
        };

        int totalMatches = 0;

        void AddCategory(string headerTitle, IEnumerable<ProjectItem> items)
        {
            var itemList = items.ToList();
            if (itemList.Count == 0) return;

            var folderItem = new TreeViewItem
            {
                Header = $"{headerTitle} ({itemList.Count})",
                IsExpanded = hasFilter,
                FontWeight = FontWeights.SemiBold
            };

            int folderMatches = 0;
            foreach (var item in itemList)
            {
                var itemNode = CreateProjectItemNode(item, filter, mode);
                if (itemNode != null)
                {
                    folderItem.Items.Add(itemNode);
                    folderMatches++;
                }
            }

            if (folderMatches > 0)
            {
                totalMatches += folderMatches;
                rootItem.Items.Add(folderItem);
            }
        }

        AddCategory("🗔 Formularios", project.Forms);
        AddCategory("📜 Módulos", project.Modules);
        AddCategory("🧩 Módulos de Clase", project.Classes);
        AddCategory("🎛️ Controles de Usuario", project.UserControls);

        // Referencias
        if (project.References.Any())
        {
            var matchingRefs = project.References
                .Where(r => !hasFilter || r.DisplayTitle.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matchingRefs.Count > 0)
            {
                var refFolder = new TreeViewItem
                {
                    Header = $"🔗 Referencias ({project.References.Count})",
                    IsExpanded = false,
                    FontWeight = FontWeights.Normal
                };

                foreach (var r in matchingRefs)
                {
                    refFolder.Items.Add(new TreeViewItem
                    {
                        Header = $"• {r.DisplayTitle}",
                        Foreground = Brushes.Gray,
                        FontWeight = FontWeights.Normal
                    });
                }
                rootItem.Items.Add(refFolder);
                totalMatches += matchingRefs.Count;
            }
        }

        if (hasFilter && totalMatches == 0 && !project.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return rootItem;
    }

    private TreeViewItem? CreateProjectItemNode(ProjectItem item, string filter, ProjectExplorerMode mode)
    {
        bool hasFilter = !string.IsNullOrEmpty(filter);
        bool fileMatches = !hasFilter || item.DisplayTitle.Contains(filter, StringComparison.OrdinalIgnoreCase);

        var node = new TreeViewItem
        {
            Header = $"{item.DisplayIcon} {item.DisplayTitle}" + (!item.FileExists ? " (No encontrado)" : ""),
            Tag = item,
            FontWeight = FontWeights.Normal,
            IsExpanded = hasFilter
        };

        if (!item.FileExists)
        {
            node.Foreground = Brushes.Crimson;
            node.ToolTip = $"El archivo no fue encontrado en: {item.AbsolutePath}";
            return fileMatches ? node : null;
        }

        node.ToolTip = item.AbsolutePath;

        // Si estamos en modo Avanzado y es un archivo de código editable, parsear miembros
        if (mode == ProjectExplorerMode.Advanced && item.IsEditable)
        {
            List<CodeMemberSymbol> members = [];
            try
            {
                string text = "";
                // Si está abierto en un tab, usar el texto en vivo
                foreach (var tabObj in DocumentsTabControl.Items)
                {
                    if (tabObj is EditorTabItem openTab &&
                        string.Equals(Path.GetFullPath(openTab.Document.FilePath), Path.GetFullPath(item.AbsolutePath), StringComparison.OrdinalIgnoreCase))
                    {
                        text = openTab.Editor.Text;
                        break;
                    }
                }

                if (string.IsNullOrEmpty(text))
                {
                    text = SafeFileStorage.LoadDocument(item.AbsolutePath).Text;
                }

                members = CodeStructureParser.ParseMembers(text, item.AbsolutePath);
            }
            catch
            {
                members = [];
            }

            int matchingMembers = 0;
            foreach (var member in members)
            {
                bool memberMatches = !hasFilter ||
                    member.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    member.DisplayText.Contains(filter, StringComparison.OrdinalIgnoreCase);

                if (memberMatches)
                {
                    matchingMembers++;
                    var memberNode = new TreeViewItem
                    {
                        Header = member.DisplayText,
                        Tag = member,
                        FontWeight = FontWeights.Normal,
                        ToolTip = $"{member.DisplayText}\nLínea {member.LineNumber}\n{member.SourceFilePath}"
                    };
                    node.Items.Add(memberNode);
                }
            }

            if (hasFilter)
            {
                if (!fileMatches && matchingMembers == 0)
                {
                    return null;
                }
                node.IsExpanded = true;
            }
        }
        else
        {
            if (hasFilter && !fileMatches)
            {
                return null;
            }
        }

        return node;
    }

    private void ProjectTreeView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ProjectTreeView.SelectedItem is TreeViewItem selItem)
        {
            if (selItem.Tag is CodeMemberSymbol sym)
            {
                var tab = OpenFile(sym.SourceFilePath);
                if (tab != null && sym.LineNumber > 0 && sym.LineNumber <= tab.Editor.Document.LineCount)
                {
                    var line = tab.Editor.Document.GetLineByNumber(sym.LineNumber);
                    tab.Editor.CaretOffset = line.Offset;
                    tab.Editor.ScrollToLine(sym.LineNumber);
                    tab.Editor.Focus();
                }
                e.Handled = true;
                return;
            }

            if (selItem.Tag is ProjectItem item)
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
                e.Handled = true;
            }
        }
    }

    private void UpdateExplorerModeUI()
    {
        bool isAdvanced = _settings.ProjectExplorerMode == ProjectExplorerMode.Advanced;
        MenuExplorerMode.IsChecked = isAdvanced;
        MenuExplorerMode.Header = isAdvanced ? "Modo de Explorador: _Avanzado (activo)" : "Modo de Explorador: _Estándar (activo)";
        TxtExplorerModeIcon.Text = isAdvanced ? "⚡" : "📁";
        BtnToggleExplorerMode.ToolTip = isAdvanced
            ? "Modo de Explorador: Avanzado (clic para cambiar a Estándar)"
            : "Modo de Explorador: Estándar (clic para cambiar a Avanzado)";
    }

    private void BtnToggleExplorerMode_Click(object sender, RoutedEventArgs e)
    {
        _settings.ProjectExplorerMode = _settings.ProjectExplorerMode == ProjectExplorerMode.Advanced
            ? ProjectExplorerMode.Standard
            : ProjectExplorerMode.Advanced;
        _settings.Save();
        UpdateExplorerModeUI();
        RefreshProjectTree();
        SetStatusMessage($"Modo de Explorador cambiado a: {(_settings.ProjectExplorerMode == ProjectExplorerMode.Advanced ? "Avanzado" : "Estándar")}.");
    }

    private void MenuExplorerMode_Click(object sender, RoutedEventArgs e)
    {
        BtnToggleExplorerMode_Click(sender, e);
    }

    private void TxtProjectSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        string filter = TxtProjectSearch.Text.Trim();
        BtnClearSearch.Visibility = string.IsNullOrEmpty(filter) ? Visibility.Collapsed : Visibility.Visible;
        RefreshProjectTree();
    }

    private void BtnClearSearch_Click(object sender, RoutedEventArgs e)
    {
        TxtProjectSearch.Text = string.Empty;
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
        if (ActiveEditor != null)
        {
            string sel = ActiveEditor.SelectedText;
            if (!string.IsNullOrWhiteSpace(sel))
            {
                ClipboardRing.Instance.Add(sel);
            }
            ActiveEditor.Cut();
        }
    }

    private void MenuCopy_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveEditor != null)
        {
            string sel = ActiveEditor.SelectedText;
            if (!string.IsNullOrWhiteSpace(sel))
            {
                ClipboardRing.Instance.Add(sel);
            }
            ActiveEditor.Copy();
        }
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
        if (MenuToggleOutput.IsChecked)
        {
            ShowBottomTab("Output");
        }
    }

    private void SetOutputPanelVisibility(bool isVisible)
    {
        if (RowOutput == null || RowOutputSplitter == null) return;
        double h = _settings.OutputPanelHeight > 50 ? _settings.OutputPanelHeight : 160;
        RowOutput.Height = isVisible ? new GridLength(h) : new GridLength(0);
        RowOutputSplitter.Height = isVisible ? new GridLength(4) : new GridLength(0);
        if (MenuToggleOutput != null) MenuToggleOutput.IsChecked = isVisible;
    }

    private void BottomTabRadio_Click(object sender, RoutedEventArgs e)
    {
        if (TabRadioOutput?.IsChecked == true) ShowBottomTab("Output");
        else if (TabRadioReferences?.IsChecked == true) ShowBottomTab("References");
        else if (TabRadioTasks?.IsChecked == true) ShowBottomTab("Tasks");
    }

    public void ShowBottomTab(string tabName)
    {
        if (ViewOutput == null || ViewReferences == null || ViewTasks == null) return;
        if (TabRadioOutput == null || TabRadioReferences == null || TabRadioTasks == null) return;

        ViewOutput.Visibility = (tabName == "Output") ? Visibility.Visible : Visibility.Collapsed;
        ViewReferences.Visibility = (tabName == "References") ? Visibility.Visible : Visibility.Collapsed;
        ViewTasks.Visibility = (tabName == "Tasks") ? Visibility.Visible : Visibility.Collapsed;

        TabRadioOutput.IsChecked = (tabName == "Output");
        TabRadioReferences.IsChecked = (tabName == "References");
        TabRadioTasks.IsChecked = (tabName == "Tasks");

        SetOutputPanelVisibility(true);
    }

    private void MenuToggleReferences_Click(object sender, RoutedEventArgs e)
    {
        ShowBottomTab("References");
    }

    private void MenuToggleTasks_Click(object sender, RoutedEventArgs e)
    {
        ShowBottomTab("Tasks");
        RefreshTaskList();
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

            UpdateExplorerModeUI();
            RefreshProjectTree();
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
        // 0. Navegación Atrás / Adelante: Alt+Left o Ctrl+- / Alt+Right o Ctrl+Shift+-
        if ((e.Key == Key.Left && Keyboard.Modifiers == ModifierKeys.Alt) ||
            (e.Key == Key.OemMinus && Keyboard.Modifiers == ModifierKeys.Control))
        {
            NavigateBackward();
            e.Handled = true;
            return;
        }

        if ((e.Key == Key.Right && Keyboard.Modifiers == ModifierKeys.Alt) ||
            (e.Key == Key.OemMinus && (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) == (ModifierKeys.Control | ModifierKeys.Shift)))
        {
            NavigateForward();
            e.Handled = true;
            return;
        }

        // Buscar todas las referencias: Shift+F12
        if (e.Key == Key.F12 && (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
        {
            FindReferencesForCurrentSymbol();
            e.Handled = true;
            return;
        }

        // Marcadores directos: Ctrl+F2 (alternar), F2 (siguiente), Shift+F2 (anterior), Ctrl+Shift+F2 (limpiar)
        if (e.Key == Key.F2)
        {
            if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) == (ModifierKeys.Control | ModifierKeys.Shift))
            {
                ClearAllBookmarks();
                e.Handled = true;
                return;
            }
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                ToggleBookmarkAtCurrentLine();
                e.Handled = true;
                return;
            }
            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
            {
                GoToPreviousBookmark();
                e.Handled = true;
                return;
            }
            if (Keyboard.Modifiers == ModifierKeys.None)
            {
                GoToNextBookmark();
                e.Handled = true;
                return;
            }
        }

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

        // 2. Secuencia acorde Ctrl+K
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
            if (e.Key == Key.K)
            {
                ToggleBookmarkAtCurrentLine();
                e.Handled = true;
                return;
            }
            if (e.Key == Key.N)
            {
                GoToNextBookmark();
                e.Handled = true;
                return;
            }
            if (e.Key == Key.P)
            {
                GoToPreviousBookmark();
                e.Handled = true;
                return;
            }
            if (e.Key == Key.L)
            {
                ClearAllBookmarks();
                e.Handled = true;
                return;
            }
            SetStatusMessage("Combinación Ctrl+K cancelada.");
        }
        else if (e.Key == Key.K && Keyboard.Modifiers == ModifierKeys.Control)
        {
            _isChordCtrlK = true;
            SetStatusMessage("Ctrl+K presionado. Esperando segunda tecla (K: Marcador, N: Siguiente, P: Anterior, L: Limpiar, C: Comentar, U: Descomentar)...");
            e.Handled = true;
            return;
        }

        // 3. Portapapeles múltiple: Ctrl+Shift+V
        if (e.Key == Key.V && (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            OpenClipboardRingDialog();
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

    private void MenuToggleEndOfLineDetails_Click(object sender, RoutedEventArgs e)
    {
        SetEndOfLineDetailsState(MenuToggleEndOfLineDetails.IsChecked);
    }

    private void BtnToolbarToggleEndOfLineDetails_Click(object sender, RoutedEventArgs e)
    {
        SetEndOfLineDetailsState(BtnToolbarToggleEndOfLineDetails.IsChecked == true);
    }

    private void SetEndOfLineDetailsState(bool enabled)
    {
        _settings.EnableEndOfLineDetails = enabled;
        MenuToggleEndOfLineDetails.IsChecked = enabled;
        BtnToolbarToggleEndOfLineDetails.IsChecked = enabled;

        foreach (var item in DocumentsTabControl.Items)
        {
            if (item is EditorTabItem tab)
            {
                tab.SetEndOfLineDetailsEnabled(enabled);
            }
        }

        SetStatusMessage(enabled ? "Detalles de fin de bloque (Inlay Hints) activados." : "Detalles de fin de bloque desactivados.");
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

    private void MenuPropertyBuilder_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveEditor == null)
        {
            MessageBox.Show("Debes abrir un archivo de código para usar el Asistente de Propiedades.",
                "Asistente de Propiedades", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        string initialProp = ActiveEditor.SelectedText.Trim();
        var dlg = new PropertyBuilderDialog(initialProp) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            InsertCodeAtCaret(dlg.GeneratedCode, "Propiedad");
        }
    }

    private void MenuProcedureBuilder_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveEditor == null)
        {
            MessageBox.Show("Debes abrir un archivo de código para usar el Asistente de Procedimientos.",
                "Asistente de Procedimientos", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        string initialName = ActiveEditor.SelectedText.Trim();
        var dlg = new ProcedureBuilderDialog(initialName) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            InsertCodeAtCaret(dlg.GeneratedCode, "Procedimiento");
        }
    }

    private void MenuInsertSnippet_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveEditor == null)
        {
            MessageBox.Show("Debes abrir un archivo de código para insertar snippets.",
                "Insertar Snippet", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var cm = new ContextMenu();
        foreach (var snip in SnippetManager.GetAllSnippets())
        {
            var mi = new MenuItem
            {
                Header = $"{snip.Title}  ({snip.Shortcut})",
                ToolTip = snip.Description,
                Tag = snip
            };
            mi.Click += (_, _) => InsertSnippetAtCaret(snip);
            cm.Items.Add(mi);
        }

        if (sender is FrameworkElement fe)
        {
            cm.PlacementTarget = fe;
            cm.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        }
        cm.IsOpen = true;
    }

    private void InsertSnippetAtCaret(SnippetDefinition snippet)
    {
        if (ActiveEditor == null || ActiveEditor.Document == null) return;

        int caretOffset = ActiveEditor.CaretOffset;
        var line = ActiveEditor.Document.GetLineByOffset(caretOffset);
        string lineTextBeforeCaret = ActiveEditor.Document.GetText(line.Offset, caretOffset - line.Offset);

        int indentLen = 0;
        while (indentLen < lineTextBeforeCaret.Length && (lineTextBeforeCaret[indentLen] == ' ' || lineTextBeforeCaret[indentLen] == '\t'))
        {
            indentLen++;
        }
        string indent = lineTextBeforeCaret[..indentLen];

        if (SnippetManager.TryExpandSnippet(snippet.Shortcut, indent, out string expanded, out int relativeCaret))
        {
            ActiveEditor.Document.Insert(caretOffset, expanded);
            ActiveEditor.CaretOffset = caretOffset + relativeCaret;
            ActiveEditor.Focus();
            SetStatusMessage($"Snippet '{snippet.Title}' insertado.");
        }
    }

    private void MenuClipboardRing_Click(object sender, RoutedEventArgs e)
    {
        OpenClipboardRingDialog();
    }

    private void OpenClipboardRingDialog()
    {
        if (ActiveEditor == null)
        {
            MessageBox.Show("Debes abrir un archivo de código para pegar desde el portapapeles.",
                "Portapapeles Múltiple", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dlg = new ClipboardRingDialog { Owner = this };
        if (dlg.ShowDialog() == true && !string.IsNullOrEmpty(dlg.SelectedText))
        {
            ActiveEditor.Document.Insert(ActiveEditor.CaretOffset, dlg.SelectedText);
            ActiveEditor.CaretOffset += dlg.SelectedText.Length;
            ActiveEditor.Focus();
            SetStatusMessage("Fragmento del portapapeles pegado.");
        }
    }

    private void InsertCodeAtCaret(string codeToInsert, string operationDesc)
    {
        if (ActiveEditor == null || ActiveEditor.Document == null || string.IsNullOrWhiteSpace(codeToInsert)) return;

        int caretOffset = ActiveEditor.CaretOffset;
        var line = ActiveEditor.Document.GetLineByOffset(caretOffset);
        string lineText = ActiveEditor.Document.GetText(line.Offset, line.Length);
        string prefix = (!string.IsNullOrWhiteSpace(lineText)) ? "\r\n\r\n" : "";
        string text = prefix + codeToInsert + "\r\n";

        ActiveEditor.Document.Insert(caretOffset, text);
        ActiveEditor.CaretOffset = caretOffset + text.Length;
        ActiveEditor.Focus();
        SetStatusMessage($"{operationDesc} insertado exitosamente.");
    }
    #region Navegación e Historial (Alt+← / Alt+→)
    private void MenuNavBackward_Click(object sender, RoutedEventArgs e)
    {
        NavigateBackward();
    }

    private void MenuNavForward_Click(object sender, RoutedEventArgs e)
    {
        NavigateForward();
    }

    public void NavigateBackward()
    {
        string curFile = ActiveTab?.Document.FilePath ?? "";
        int curLine = ActiveEditor?.TextArea.Caret.Line ?? 1;
        int curCol = ActiveEditor?.TextArea.Caret.Column ?? 1;

        var target = NavigationHistory.Instance.NavigateBackward(curFile, curLine, curCol);
        if (target != null)
        {
            var tab = OpenFile(target.FilePath);
            if (tab != null && target.LineNumber > 0 && target.LineNumber <= tab.Editor.Document.LineCount)
            {
                var lineObj = tab.Editor.Document.GetLineByNumber(target.LineNumber);
                int targetOffset = Math.Min(lineObj.Offset + Math.Max(0, target.ColumnNumber - 1), lineObj.EndOffset);
                tab.Editor.CaretOffset = targetOffset;
                tab.Editor.ScrollToLine(target.LineNumber);
                tab.Editor.Focus();
                SetStatusMessage($"Navegación atrás: {Path.GetFileName(target.FilePath)} (Línea {target.LineNumber})");
            }
        }
        else
        {
            SetStatusMessage("No hay más ubicaciones anteriores en el historial de navegación.");
        }
    }

    public void NavigateForward()
    {
        string curFile = ActiveTab?.Document.FilePath ?? "";
        int curLine = ActiveEditor?.TextArea.Caret.Line ?? 1;
        int curCol = ActiveEditor?.TextArea.Caret.Column ?? 1;

        var target = NavigationHistory.Instance.NavigateForward(curFile, curLine, curCol);
        if (target != null)
        {
            var tab = OpenFile(target.FilePath);
            if (tab != null && target.LineNumber > 0 && target.LineNumber <= tab.Editor.Document.LineCount)
            {
                var lineObj = tab.Editor.Document.GetLineByNumber(target.LineNumber);
                int targetOffset = Math.Min(lineObj.Offset + Math.Max(0, target.ColumnNumber - 1), lineObj.EndOffset);
                tab.Editor.CaretOffset = targetOffset;
                tab.Editor.ScrollToLine(target.LineNumber);
                tab.Editor.Focus();
                SetStatusMessage($"Navegación adelante: {Path.GetFileName(target.FilePath)} (Línea {target.LineNumber})");
            }
        }
        else
        {
            SetStatusMessage("No hay más ubicaciones posteriores en el historial de navegación.");
        }
    }
    #endregion

    #region Marcadores (Bookmarks)
    private void MenuToggleBookmark_Click(object sender, RoutedEventArgs e)
    {
        ToggleBookmarkAtCurrentLine();
    }

    private void MenuNextBookmark_Click(object sender, RoutedEventArgs e)
    {
        GoToNextBookmark();
    }

    private void MenuPrevBookmark_Click(object sender, RoutedEventArgs e)
    {
        GoToPreviousBookmark();
    }

    private void MenuClearBookmarks_Click(object sender, RoutedEventArgs e)
    {
        ClearAllBookmarks();
    }

    public void ToggleBookmarkAtCurrentLine()
    {
        if (ActiveTab == null) return;
        int line = ActiveTab.Editor.TextArea.Caret.Line;
        bool added = BookmarkManager.Instance.ToggleBookmark(ActiveTab.Document.FilePath, line);
        ActiveTab.BookmarkMargin?.InvalidateVisual();
        SetStatusMessage(added ? $"Marcador agregado en línea {line}." : $"Marcador eliminado de línea {line}.");
    }

    public void GoToNextBookmark()
    {
        string curFile = ActiveTab?.Document.FilePath ?? "";
        int curLine = ActiveEditor?.TextArea.Caret.Line ?? 0;

        var next = BookmarkManager.Instance.GetNextBookmark(curFile, curLine);
        if (next != null)
        {
            RecordCurrentNavigation();
            var tab = OpenFile(next.Value.FilePath);
            if (tab != null && next.Value.Line <= tab.Editor.Document.LineCount)
            {
                var lineObj = tab.Editor.Document.GetLineByNumber(next.Value.Line);
                tab.Editor.CaretOffset = lineObj.Offset;
                tab.Editor.ScrollToLine(next.Value.Line);
                tab.Editor.Focus();
                SetStatusMessage($"Marcador: {Path.GetFileName(next.Value.FilePath)} (Línea {next.Value.Line})");
            }
        }
        else
        {
            SetStatusMessage("No hay marcadores activos en el proyecto.");
        }
    }

    public void GoToPreviousBookmark()
    {
        string curFile = ActiveTab?.Document.FilePath ?? "";
        int curLine = ActiveEditor?.TextArea.Caret.Line ?? 0;

        var prev = BookmarkManager.Instance.GetPreviousBookmark(curFile, curLine);
        if (prev != null)
        {
            RecordCurrentNavigation();
            var tab = OpenFile(prev.Value.FilePath);
            if (tab != null && prev.Value.Line <= tab.Editor.Document.LineCount)
            {
                var lineObj = tab.Editor.Document.GetLineByNumber(prev.Value.Line);
                tab.Editor.CaretOffset = lineObj.Offset;
                tab.Editor.ScrollToLine(prev.Value.Line);
                tab.Editor.Focus();
                SetStatusMessage($"Marcador: {Path.GetFileName(prev.Value.FilePath)} (Línea {prev.Value.Line})");
            }
        }
        else
        {
            SetStatusMessage("No hay marcadores activos en el proyecto.");
        }
    }

    public void ClearAllBookmarks()
    {
        BookmarkManager.Instance.ClearAllBookmarks();
        foreach (var item in DocumentsTabControl.Items)
        {
            if (item is EditorTabItem tab)
            {
                tab.BookmarkMargin?.InvalidateVisual();
            }
        }
        SetStatusMessage("Todos los marcadores han sido borrados.");
    }
    #endregion

    #region Búsqueda de Referencias (Find References)
    private void MenuFindReferences_Click(object sender, RoutedEventArgs e)
    {
        FindReferencesForCurrentSymbol();
    }

    private void BtnSearchReferencesPrompt_Click(object sender, RoutedEventArgs e)
    {
        FindReferencesForCurrentSymbol();
    }

    private void BtnClearReferences_Click(object sender, RoutedEventArgs e)
    {
        LstReferences.ItemsSource = null;
        TxtReferencesStatus.Text = "Búsqueda limpiada.";
    }

    public void FindReferencesForCurrentSymbol()
    {
        string symbol = ActiveTab?.GetWordUnderCaret() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(symbol))
        {
            var dlg = new InputPromptDialog("Buscar todas las referencias", "Ingresa el nombre del símbolo, rutina o variable a buscar:")
            {
                Owner = this
            };
            if (dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(dlg.InputValue))
            {
                symbol = dlg.InputValue;
            }
            else
            {
                return;
            }
        }

        var projectFiles = GetSearchableFiles();
        if (projectFiles.Count == 0)
        {
            MessageBox.Show("Debes abrir un proyecto o archivo de código para buscar referencias.", "Buscar Referencias", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        SetStatusMessage($"Buscando referencias de '{symbol}'...");

        var results = ReferenceFinder.FindReferences(symbol, projectFiles, path => GetOpenDocumentText(path));
        LstReferences.ItemsSource = results;
        int fileCount = results.Select(r => r.FilePath).Distinct().Count();
        TxtReferencesStatus.Text = $"Referencias de '{symbol}': {results.Count} coincidencia(s) encontrada(s) en {fileCount} archivo(s).";

        ShowBottomTab("References");
        SetStatusMessage($"Referencias de '{symbol}': {results.Count} encontrada(s).");
    }

    private void LstReferences_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (LstReferences.SelectedItem is ReferenceMatch match)
        {
            RecordCurrentNavigation();
            var tab = OpenFile(match.FilePath);
            if (tab != null && match.LineNumber > 0 && match.LineNumber <= tab.Editor.Document.LineCount)
            {
                var lineObj = tab.Editor.Document.GetLineByNumber(match.LineNumber);
                int tokenOffset = lineObj.Offset + Math.Max(0, match.ColumnNumber - 1);
                tab.Editor.CaretOffset = tokenOffset;
                tab.Editor.ScrollToLine(match.LineNumber);
                tab.Editor.Focus();
            }
        }
    }
    #endregion

    #region Explorador de Tareas (TODO Explorer)
    private List<TaskItem> _allTasks = [];

    private void BtnRefreshTasks_Click(object sender, RoutedEventArgs e)
    {
        RefreshTaskList();
    }

    private void CmbTaskFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplyTaskFilter();
    }

    public void RefreshTaskList()
    {
        var projectFiles = GetSearchableFiles();
        if (projectFiles.Count == 0)
        {
            LstTasks.ItemsSource = null;
            TxtTasksStatus.Text = "No hay archivos de proyecto cargados.";
            return;
        }

        SetStatusMessage("Escaneando tareas en el proyecto...");
        _allTasks = TaskScanner.ScanTasks(projectFiles, path => GetOpenDocumentText(path));
        ApplyTaskFilter();
        SetStatusMessage($"Lista de tareas actualizada: {_allTasks.Count} tarea(s) encontrada(s).");
    }

    private void ApplyTaskFilter()
    {
        if (LstTasks == null || TxtTasksStatus == null) return;

        string filter = (CmbTaskFilter?.SelectedItem is ComboBoxItem cbi) ? (cbi.Content?.ToString() ?? "Todas") : "Todas";

        IEnumerable<TaskItem> filtered = _allTasks;
        if (filter != "Todas")
        {
            filtered = _allTasks.Where(t => string.Equals(t.Tag, filter, StringComparison.OrdinalIgnoreCase));
        }

        var list = filtered.ToList();
        LstTasks.ItemsSource = list;
        TxtTasksStatus.Text = $"{list.Count} tarea(s) visible(s) ({_allTasks.Count} en total).";
    }

    private void LstTasks_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (LstTasks.SelectedItem is TaskItem task)
        {
            RecordCurrentNavigation();
            var tab = OpenFile(task.FilePath);
            if (tab != null && task.LineNumber > 0 && task.LineNumber <= tab.Editor.Document.LineCount)
            {
                var lineObj = tab.Editor.Document.GetLineByNumber(task.LineNumber);
                tab.Editor.CaretOffset = lineObj.Offset;
                tab.Editor.ScrollToLine(task.LineNumber);
                tab.Editor.Focus();
            }
        }
    }
    #endregion

    #region Utilidades de Navegación y Documentos
    private List<string> GetSearchableFiles()
    {
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (_currentProjectGroup != null)
        {
            foreach (var proj in _currentProjectGroup.Projects)
            {
                foreach (var item in proj.Items)
                {
                    if (item.IsEditable && !string.IsNullOrWhiteSpace(item.AbsolutePath))
                    {
                        files.Add(item.AbsolutePath);
                    }
                }
            }
        }
        else if (_currentProject != null)
        {
            foreach (var item in _currentProject.Items)
            {
                if (item.IsEditable && !string.IsNullOrWhiteSpace(item.AbsolutePath))
                {
                    files.Add(item.AbsolutePath);
                }
            }
        }

        foreach (var item in DocumentsTabControl.Items)
        {
            if (item is EditorTabItem tab && !string.IsNullOrWhiteSpace(tab.Document.FilePath))
            {
                files.Add(tab.Document.FilePath);
            }
        }

        return files.ToList();
    }

    private string? GetOpenDocumentText(string filePath)
    {
        foreach (var item in DocumentsTabControl.Items)
        {
            if (item is EditorTabItem tab && string.Equals(Path.GetFullPath(tab.Document.FilePath), Path.GetFullPath(filePath), StringComparison.OrdinalIgnoreCase))
            {
                return tab.Editor.Text;
            }
        }
        return null;
    }

    private void RecordCurrentNavigation()
    {
        if (ActiveTab != null)
        {
            int line = ActiveEditor?.TextArea.Caret.Line ?? 1;
            int col = ActiveEditor?.TextArea.Caret.Column ?? 1;
            NavigationHistory.Instance.RecordNavigation(ActiveTab.Document.FilePath, line, col);
        }
    }

    public void CommentSelection()
    {
        if (ActiveEditor != null)
        {
            EditorOperationsHelper.CommentSelection(ActiveEditor);
            SetStatusMessage("Líneas comentadas.");
        }
    }

    public void UncommentSelection()
    {
        if (ActiveEditor != null)
        {
            EditorOperationsHelper.UncommentSelection(ActiveEditor);
            SetStatusMessage("Líneas descomentadas.");
        }
    }
    #endregion
    #endregion

    private void BtnCloseTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is EditorTabItem tabItem)
        {
            CloseTab(tabItem);
        }
    }
}