using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using ViBa7.ProjectModel.Designers;

namespace ViBa7.IDE.Designer.MenuEditor;

/// <summary>
/// Diálogo modal del Editor de Menús clásico de Visual Basic 6.0.
/// Permite crear, estructurar y editar la barra de menús del formulario activo.
/// </summary>
public partial class MenuEditorWindow : Window
{
    public class MenuItemViewModel : INotifyPropertyChanged
    {
        private string _caption = string.Empty;
        private string _name = string.Empty;
        private int? _index;
        private string _shortcut = string.Empty;
        private int _helpContextID;
        private int _negotiatePosition;
        private bool _checked;
        private bool _enabled = true;
        private bool _visible = true;
        private bool _windowList;
        private int _level;

        public FormDesignerControl Control { get; }

        public string Caption
        {
            get => _caption;
            set
            {
                _caption = value;
                OnPropertyChanged(nameof(Caption));
                OnPropertyChanged(nameof(IndentedCaption));
                OnPropertyChanged(nameof(DisplayText));
            }
        }

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(nameof(Name)); }
        }

        public int? Index
        {
            get => _index;
            set { _index = value; OnPropertyChanged(nameof(Index)); }
        }

        public string Shortcut
        {
            get => _shortcut;
            set
            {
                _shortcut = value;
                OnPropertyChanged(nameof(Shortcut));
                OnPropertyChanged(nameof(ShortcutDisplay));
                OnPropertyChanged(nameof(DisplayText));
            }
        }

        public int HelpContextID
        {
            get => _helpContextID;
            set { _helpContextID = value; OnPropertyChanged(nameof(HelpContextID)); }
        }

        public int NegotiatePosition
        {
            get => _negotiatePosition;
            set { _negotiatePosition = value; OnPropertyChanged(nameof(NegotiatePosition)); }
        }

        public bool Checked
        {
            get => _checked;
            set { _checked = value; OnPropertyChanged(nameof(Checked)); }
        }

        public bool Enabled
        {
            get => _enabled;
            set { _enabled = value; OnPropertyChanged(nameof(Enabled)); }
        }

        public bool Visible
        {
            get => _visible;
            set { _visible = value; OnPropertyChanged(nameof(Visible)); }
        }

        public bool WindowList
        {
            get => _windowList;
            set { _windowList = value; OnPropertyChanged(nameof(WindowList)); }
        }

        public int Level
        {
            get => _level;
            set
            {
                _level = Math.Max(0, value);
                OnPropertyChanged(nameof(Level));
                OnPropertyChanged(nameof(IndentedCaption));
                OnPropertyChanged(nameof(DisplayText));
            }
        }

        public string IndentedCaption
        {
            get
            {
                string indentDots = new string('.', Level * 4);
                string text = string.IsNullOrEmpty(Caption) ? "(Menú sin título)" : Caption;
                return $"{indentDots}{text}";
            }
        }

        public string ShortcutDisplay
        {
            get
            {
                if (string.IsNullOrEmpty(Shortcut) || Shortcut.Equals("(None)", StringComparison.OrdinalIgnoreCase))
                    return string.Empty;
                return VbMenuShortcutHelper.ToDisplayText(Shortcut);
            }
        }

        public string DisplayText => $"{IndentedCaption}{(string.IsNullOrEmpty(ShortcutDisplay) ? "" : $"   [{ShortcutDisplay}]")}";

        public MenuItemViewModel(FormDesignerControl control, int level = 0)
        {
            Control = control;
            _caption = control.Caption;
            _name = control.Name;
            _index = control.Index;
            _shortcut = control.Shortcut;
            _helpContextID = control.HelpContextID;
            _negotiatePosition = control.NegotiatePosition;
            _checked = control.Checked;
            _enabled = control.Enabled;
            _visible = control.Visible;
            _windowList = control.WindowList;
            _level = level;
        }

        public void ApplyToControl()
        {
            Control.TypeName = "VB.Menu";
            Control.Name = Name;
            Control.Caption = Caption;
            Control.Index = Index;
            Control.Shortcut = VbMenuShortcutHelper.ToVbCode(Shortcut);
            Control.HelpContextID = HelpContextID;
            Control.NegotiatePosition = NegotiatePosition;
            Control.Checked = Checked;
            Control.Enabled = Enabled;
            Control.Visible = Visible;
            Control.WindowList = WindowList;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));

        public override string ToString() => DisplayText;
    }

    private readonly FormDesignModel _model;
    private readonly ObservableCollection<MenuItemViewModel> _items = new();
    private bool _isUpdatingUI = true;

    public MenuEditorWindow(FormDesignModel model)
    {
        _isUpdatingUI = true;
        InitializeComponent();
        _model = model;

        // Cargar catálogo de atajos en el ComboBox
        CmbShortcut.ItemsSource = VbMenuShortcutHelper.AvailableShortcuts;
        CmbShortcut.SelectedIndex = 0;

        // Cargar menús existentes del modelo
        var existing = _model.GetFlattenedMenuItems();
        if (existing.Count > 0)
        {
            foreach (var (ctrl, level) in existing)
            {
                _items.Add(new MenuItemViewModel(ctrl.Clone(), level));
            }
        }
        else
        {
            // Menú inicial por defecto
            var initialCtrl = new FormDesignerControl
            {
                TypeName = "VB.Menu",
                Name = "mnuItem1",
                Caption = "&Menu1"
            };
            _items.Add(new MenuItemViewModel(initialCtrl, 0));
        }

        LstMenuItems.ItemsSource = _items;
        if (_items.Count > 0)
        {
            LstMenuItems.SelectedIndex = 0;
        }

        _isUpdatingUI = false;
        UpdateFormFieldsFromSelection();

        Loaded += (_, _) => TxtCaption.Focus();
    }

    private MenuItemViewModel? SelectedItem => LstMenuItems?.SelectedItem as MenuItemViewModel;

    private void LstMenuItems_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateFormFieldsFromSelection();
    }

    private void UpdateFormFieldsFromSelection()
    {
        var item = SelectedItem;
        if (item == null) return;

        _isUpdatingUI = true;
        try
        {
            TxtCaption.Text = item.Caption;
            TxtName.Text = item.Name;
            TxtIndex.Text = item.Index.HasValue ? item.Index.Value.ToString() : string.Empty;

            string displayShortcut = VbMenuShortcutHelper.ToDisplayText(item.Shortcut);
            int shortcutIdx = CmbShortcut.Items.IndexOf(displayShortcut);
            CmbShortcut.SelectedIndex = shortcutIdx >= 0 ? shortcutIdx : 0;

            TxtHelpContextID.Text = item.HelpContextID > 0 ? item.HelpContextID.ToString() : string.Empty;
            CmbNegotiatePosition.SelectedIndex = Math.Clamp(item.NegotiatePosition, 0, 3);

            ChkChecked.IsChecked = item.Checked;
            ChkEnabled.IsChecked = item.Enabled;
            ChkVisible.IsChecked = item.Visible;
            ChkWindowList.IsChecked = item.WindowList;

            // Habilitar/Deshabilitar botones de navegación según posición
            int index = LstMenuItems.SelectedIndex;
            BtnOutdent.IsEnabled = item.Level > 0;
            BtnIndent.IsEnabled = index > 0 && item.Level <= _items[index - 1].Level;
            BtnUp.IsEnabled = index > 0;
            BtnDown.IsEnabled = index < _items.Count - 1;
        }
        finally
        {
            _isUpdatingUI = false;
        }
    }

    private void TxtCaption_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingUI || SelectedItem == null) return;
        SelectedItem.Caption = TxtCaption.Text;
    }

    private void TxtName_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingUI || SelectedItem == null) return;
        SelectedItem.Name = TxtName.Text.Trim();
    }

    private void TxtIndex_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingUI || SelectedItem == null) return;
        if (int.TryParse(TxtIndex.Text.Trim(), out int val) && val >= 0)
        {
            SelectedItem.Index = val;
        }
        else
        {
            SelectedItem.Index = null;
        }
    }

    private void CmbShortcut_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingUI || SelectedItem == null) return;
        string? selected = CmbShortcut.SelectedItem as string;
        SelectedItem.Shortcut = VbMenuShortcutHelper.ToVbCode(selected);
    }

    private void TxtHelpContextID_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingUI || SelectedItem == null) return;
        if (int.TryParse(TxtHelpContextID.Text.Trim(), out int val))
        {
            SelectedItem.HelpContextID = val;
        }
        else
        {
            SelectedItem.HelpContextID = 0;
        }
    }

    private void CmbNegotiatePosition_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingUI || SelectedItem == null) return;
        SelectedItem.NegotiatePosition = Math.Max(0, CmbNegotiatePosition.SelectedIndex);
    }

    private void ChkProperty_Changed(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingUI || SelectedItem == null) return;
        SelectedItem.Checked = ChkChecked.IsChecked == true;
        SelectedItem.Enabled = ChkEnabled.IsChecked == true;
        SelectedItem.Visible = ChkVisible.IsChecked == true;
        SelectedItem.WindowList = ChkWindowList.IsChecked == true;
    }

    #region Operaciones de Estructura (Indent, Outdent, Mover, Insertar, Borrar)
    private void BtnOutdent_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedItem == null || SelectedItem.Level <= 0) return;
        SelectedItem.Level--;
        UpdateFormFieldsFromSelection();
    }

    private void BtnIndent_Click(object sender, RoutedEventArgs e)
    {
        int idx = LstMenuItems.SelectedIndex;
        if (idx <= 0 || SelectedItem == null) return;

        // No se puede indentar más de 1 nivel por encima del elemento inmediatamente anterior
        int prevLevel = _items[idx - 1].Level;
        if (SelectedItem.Level <= prevLevel)
        {
            SelectedItem.Level++;
            UpdateFormFieldsFromSelection();
        }
    }

    private void BtnUp_Click(object sender, RoutedEventArgs e)
    {
        int idx = LstMenuItems.SelectedIndex;
        if (idx <= 0) return;

        var item = _items[idx];
        _items.RemoveAt(idx);
        _items.Insert(idx - 1, item);
        if (idx - 1 == 0 && item.Level > 0)
        {
            item.Level = 0;
        }
        LstMenuItems.SelectedIndex = idx - 1;
        UpdateFormFieldsFromSelection();
    }

    private void BtnDown_Click(object sender, RoutedEventArgs e)
    {
        int idx = LstMenuItems.SelectedIndex;
        if (idx < 0 || idx >= _items.Count - 1) return;

        var item = _items[idx];
        _items.RemoveAt(idx);
        _items.Insert(idx + 1, item);
        if (_items[0].Level > 0)
        {
            _items[0].Level = 0;
        }
        LstMenuItems.SelectedIndex = idx + 1;
        UpdateFormFieldsFromSelection();
    }

    private void BtnNext_Click(object sender, RoutedEventArgs e)
    {
        int idx = LstMenuItems.SelectedIndex;
        if (idx < _items.Count - 1)
        {
            LstMenuItems.SelectedIndex = idx + 1;
            TxtCaption.Focus();
            TxtCaption.SelectAll();
        }
        else
        {
            // Crear nuevo elemento al final con el mismo nivel
            int currentLevel = SelectedItem != null ? SelectedItem.Level : 0;
            string newName = GenerateNextMenuName();
            var newCtrl = new FormDesignerControl
            {
                TypeName = "VB.Menu",
                Name = newName,
                Caption = string.Empty
            };
            var newItem = new MenuItemViewModel(newCtrl, currentLevel);
            _items.Add(newItem);
            LstMenuItems.SelectedIndex = _items.Count - 1;
            TxtCaption.Focus();
        }
    }

    private void BtnInsert_Click(object sender, RoutedEventArgs e)
    {
        int idx = Math.Max(0, LstMenuItems.SelectedIndex);
        int currentLevel = (idx == 0) ? 0 : (SelectedItem != null ? SelectedItem.Level : 0);
        string newName = GenerateNextMenuName();
        var newCtrl = new FormDesignerControl
        {
            TypeName = "VB.Menu",
            Name = newName,
            Caption = string.Empty
        };
        var newItem = new MenuItemViewModel(newCtrl, currentLevel);
        _items.Insert(idx, newItem);
        LstMenuItems.SelectedIndex = idx;
        TxtCaption.Focus();
    }

    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        int idx = LstMenuItems.SelectedIndex;
        if (idx < 0) return;

        _items.RemoveAt(idx);

        if (_items.Count == 0)
        {
            var initialCtrl = new FormDesignerControl
            {
                TypeName = "VB.Menu",
                Name = "mnuItem1",
                Caption = string.Empty
            };
            _items.Add(new MenuItemViewModel(initialCtrl, 0));
        }

        int nextIdx = Math.Min(idx, _items.Count - 1);
        LstMenuItems.SelectedIndex = nextIdx;
        TxtCaption.Focus();
    }

    private string GenerateNextMenuName()
    {
        var existingNames = new HashSet<string>(_items.Select(i => i.Name), StringComparer.OrdinalIgnoreCase);
        int counter = 1;
        while (existingNames.Contains($"mnuItem{counter}"))
        {
            counter++;
        }
        return $"mnuItem{counter}";
    }
    #endregion

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        // Si solo hay un elemento inicial sin Caption, se considera menú vacío (quitar todos los menús)
        if (_items.Count == 1 && string.IsNullOrWhiteSpace(_items[0].Caption))
        {
            _model.ReplaceMenus(Enumerable.Empty<(FormDesignerControl, int)>());
            DialogResult = true;
            Close();
            return;
        }

        // 1. Validar elementos
        for (int i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            if (string.IsNullOrWhiteSpace(item.Name))
            {
                MessageBox.Show($"El elemento de menú en la posición {i + 1} no tiene un Nombre (Name) asignado.",
                    "Editor de Menús", MessageBoxButton.OK, MessageBoxImage.Warning);
                LstMenuItems.SelectedIndex = i;
                TxtName.Focus();
                return;
            }

            if (!Regex.IsMatch(item.Name, @"^[a-zA-Z_][a-zA-Z0-9_]*$"))
            {
                MessageBox.Show($"El nombre '{item.Name}' no es un identificador válido de Visual Basic.",
                    "Editor de Menús", MessageBoxButton.OK, MessageBoxImage.Warning);
                LstMenuItems.SelectedIndex = i;
                TxtName.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(item.Caption))
            {
                MessageBox.Show($"El elemento de menú '{item.Name}' debe tener un Título (Caption). Usa '-' para un separador.",
                    "Editor de Menús", MessageBoxButton.OK, MessageBoxImage.Warning);
                LstMenuItems.SelectedIndex = i;
                TxtCaption.Focus();
                return;
            }
        }

        // 2. Validar que no haya nombres duplicados (salvo si usan Index diferente para Control Arrays)
        var nameGroups = _items.GroupBy(i => i.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var group in nameGroups)
        {
            if (group.Count() > 1)
            {
                bool allHaveUniqueIndices = group.All(i => i.Index.HasValue) &&
                                            group.Select(i => i.Index!.Value).Distinct().Count() == group.Count();
                if (!allHaveUniqueIndices)
                {
                    MessageBox.Show($"El nombre de menú '{group.Key}' está duplicado sin índices únicos de arreglo de controles.",
                        "Editor de Menús", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }
        }

        // 3. Aplicar propiedades a cada control y reconstruir jerarquía en el modelo
        var flattenedList = new List<(FormDesignerControl Control, int Level)>();
        foreach (var item in _items)
        {
            item.ApplyToControl();
            flattenedList.Add((item.Control, item.Level));
        }

        _model.ReplaceMenus(flattenedList);

        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
