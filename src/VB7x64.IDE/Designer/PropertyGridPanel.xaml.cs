using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using VB7x64.ProjectModel.Designers;

namespace VB7x64.IDE.Designer;

public class PropertyTemplateSelector : DataTemplateSelector
{
    public DataTemplate? TextTemplate { get; set; }
    public DataTemplate? ChoiceTemplate { get; set; }
    public DataTemplate? ColorTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
    {
        if (item is PropertyItemViewModel prop)
        {
            if (prop.Type == DesignerPropertyType.ColorHex)
                return ColorTemplate;
            if (prop.Choices.Count > 0)
                return ChoiceTemplate;
            return TextTemplate;
        }
        return base.SelectTemplate(item, container);
    }
}

public class PropertyItemViewModel : INotifyPropertyChanged
{
    private readonly DesignerPropertyItem _model;
    private readonly Action _onModelChanged;

    public string Name => _model.Name;
    public string DisplayName => _model.DisplayName;
    public string Category => _model.Category;
    public string Description => _model.Description;
    public DesignerPropertyType Type => _model.Type;
    public List<DesignerPropertyChoice> Choices => _model.Choices;
    public bool IsReadOnly => _model.IsReadOnly;

    public string CurrentValue
    {
        get => _model.CurrentValue;
        set
        {
            if (_model.CurrentValue != value)
            {
                if (_model.SetValue(value))
                {
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentValue)));
                    if (Type == DesignerPropertyType.ColorHex)
                    {
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ColorBrush)));
                    }
                    _onModelChanged();
                }
            }
        }
    }

    public Brush ColorBrush
    {
        get
        {
            if (Type == DesignerPropertyType.ColorHex)
            {
                return VbColorHelper.GetBrushFromHex(CurrentValue, VbColorModel.DefaultControlColor);
            }
            return Brushes.Transparent;
        }
    }

    public PropertyItemViewModel(DesignerPropertyItem model, Action onModelChanged)
    {
        _model = model;
        _onModelChanged = onModelChanged;
    }

    public void Refresh()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentValue)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ColorBrush)));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public class ObjectSelectorItem
{
    public string DisplayName { get; init; } = string.Empty;
    public object Target { get; init; } = null!;
}

public partial class PropertyGridPanel : UserControl
{
    private FormDesignModel? _model;
    private object? _currentSelectedTarget;
    private readonly List<PropertyItemViewModel> _allProperties = new();
    private bool _isUpdatingSelectionInternal = false;

    public event Action<FormDesignerControl?>? ObjectSelected;
    public event Action? ModelChanged;

    public PropertyGridPanel()
    {
        InitializeComponent();
    }

    public void BindModel(FormDesignModel model, FormDesignerControl? selectedControl = null)
    {
        _model = model;
        RefreshObjectList();

        if (selectedControl != null)
        {
            SelectControl(selectedControl);
        }
        else
        {
            SelectForm();
        }
    }

    public void RefreshObjectList()
    {
        if (_model == null || CmbObjects == null) return;

        var items = new List<ObjectSelectorItem>
        {
            new ObjectSelectorItem
            {
                DisplayName = $"{_model.Name} (Form)",
                Target = _model
            }
        };

        foreach (var ctrl in _model.GetAllControls().OrderBy(c => c.Name))
        {
            items.Add(new ObjectSelectorItem
            {
                DisplayName = $"{ctrl.Name} ({ctrl.DisplayType})",
                Target = ctrl
            });
        }

        _isUpdatingSelectionInternal = true;
        CmbObjects.ItemsSource = items;

        if (_currentSelectedTarget is FormDesignerControl ctrlTarget)
        {
            CmbObjects.SelectedItem = items.FirstOrDefault(i => i.Target == ctrlTarget) ?? items.FirstOrDefault();
        }
        else
        {
            CmbObjects.SelectedItem = items.FirstOrDefault();
        }
        _isUpdatingSelectionInternal = false;
    }

    public void SelectForm()
    {
        if (_model == null || CmbObjects == null) return;
        _currentSelectedTarget = _model;

        _isUpdatingSelectionInternal = true;
        if (CmbObjects.ItemsSource is IEnumerable<ObjectSelectorItem> items)
        {
            CmbObjects.SelectedItem = items.FirstOrDefault(i => i.Target == _model);
        }
        _isUpdatingSelectionInternal = false;

        if (TxtHeaderTitle != null)
        {
            TxtHeaderTitle.Text = $"Propiedades - {_model.Name}";
        }
        LoadPropertiesForTarget(_model);
    }

    public void SelectControl(FormDesignerControl? control)
    {
        if (control == null)
        {
            SelectForm();
            return;
        }

        if (CmbObjects == null) return;

        _currentSelectedTarget = control;

        _isUpdatingSelectionInternal = true;
        if (CmbObjects.ItemsSource is IEnumerable<ObjectSelectorItem> items)
        {
            CmbObjects.SelectedItem = items.FirstOrDefault(i => i.Target == control);
        }
        _isUpdatingSelectionInternal = false;

        if (TxtHeaderTitle != null)
        {
            TxtHeaderTitle.Text = $"Propiedades - {control.Name}";
        }
        LoadPropertiesForTarget(control);
    }

    public void RefreshCurrentProperties()
    {
        foreach (var prop in _allProperties)
        {
            prop.Refresh();
        }
    }

    private void LoadPropertiesForTarget(object target)
    {
        if (_model == null) return;

        _allProperties.Clear();

        if (target is FormDesignModel formModel)
        {
            var raw = DesignerPropertyFactory.GetPropertiesForForm(formModel, OnInnerModelChanged);
            foreach (var p in raw)
            {
                _allProperties.Add(new PropertyItemViewModel(p, OnInnerModelChanged));
            }
        }
        else if (target is FormDesignerControl ctrl)
        {
            var raw = DesignerPropertyFactory.GetPropertiesForControl(ctrl, _model, OnInnerModelChanged);
            foreach (var p in raw)
            {
                _allProperties.Add(new PropertyItemViewModel(p, OnInnerModelChanged));
            }
        }

        ApplyFilterAndSorting();
    }

    private void OnInnerModelChanged()
    {
        RefreshObjectList();
        ModelChanged?.Invoke();
    }

    private void ApplyFilterAndSorting()
    {
        if (TxtFilter == null || RadioCategorized == null || PropertiesListView == null)
        {
            return;
        }

        IEnumerable<PropertyItemViewModel> query = _allProperties;

        string filter = TxtFilter.Text.Trim();
        if (!string.IsNullOrEmpty(filter))
        {
            query = query.Where(p =>
                p.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                p.Category.Contains(filter, StringComparison.OrdinalIgnoreCase)
            );
        }

        if (RadioCategorized.IsChecked == true)
        {
            query = query.OrderBy(p => p.Category).ThenBy(p => p.DisplayName);
        }
        else
        {
            query = query.OrderBy(p => p.DisplayName.StartsWith("(") ? 0 : 1).ThenBy(p => p.DisplayName);
        }

        PropertiesListView.ItemsSource = query.ToList();
    }

    private void CmbObjects_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingSelectionInternal) return;
        if (CmbObjects == null || TxtHeaderTitle == null) return;

        if (CmbObjects.SelectedItem is ObjectSelectorItem item)
        {
            _currentSelectedTarget = item.Target;
            if (item.Target is FormDesignModel)
            {
                TxtHeaderTitle.Text = $"Propiedades - {_model?.Name}";
                LoadPropertiesForTarget(item.Target);
                ObjectSelected?.Invoke(null);
            }
            else if (item.Target is FormDesignerControl ctrl)
            {
                TxtHeaderTitle.Text = $"Propiedades - {ctrl.Name}";
                LoadPropertiesForTarget(ctrl);
                ObjectSelected?.Invoke(ctrl);
            }
        }
    }

    private void RadioViewMode_Checked(object sender, RoutedEventArgs e)
    {
        ApplyFilterAndSorting();
    }

    private void TxtFilter_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilterAndSorting();
    }

    private void PropertiesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PropertiesListView == null || TxtHelpPropName == null || TxtHelpDescription == null) return;

        if (PropertiesListView.SelectedItem is PropertyItemViewModel prop)
        {
            TxtHelpPropName.Text = prop.DisplayName;
            TxtHelpDescription.Text = prop.Description;
        }
        else
        {
            TxtHelpPropName.Text = "Propiedad";
            TxtHelpDescription.Text = "Selecciona una propiedad para ver su descripción.";
        }
    }

    private void PropertyTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && sender is TextBox tb)
        {
            var binding = tb.GetBindingExpression(TextBox.TextProperty);
            binding?.UpdateSource();
            Keyboard.ClearFocus();
            e.Handled = true;
        }
    }

    private void PropertyTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb)
        {
            var binding = tb.GetBindingExpression(TextBox.TextProperty);
            binding?.UpdateSource();
        }
    }

    private void PropertyChoice_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox cb)
        {
            var binding = cb.GetBindingExpression(ComboBox.SelectedValueProperty);
            binding?.UpdateSource();
        }
    }

    private void BtnPickColor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is PropertyItemViewModel prop)
        {
            ShowColorPaletteMenu(btn, prop);
        }
    }

    private void ShowColorPaletteMenu(Button anchor, PropertyItemViewModel prop)
    {
        var menu = new ContextMenu();

        var colors = new (string Name, string Hex)[]
        {
            // Colores del sistema
            ("ButtonFace (&H8000000F&)", "&H8000000F&"),
            ("Window Blanco (&H80000005&)", "&H80000005&"),
            ("WindowText Negro (&H80000008&)", "&H80000008&"),
            ("ButtonHighlight (&H80000014&)", "&H80000014&"),
            ("ButtonShadow (&H80000010&)", "&H80000010&"),
            ("Highlight Azul (&H8000000D&)", "&H8000000D&"),
            // Colores RGB Clásicos VB6
            ("Negro (vbBlack)", "&H00000000&"),
            ("Blanco (vbWhite)", "&H00FFFFFF&"),
            ("Rojo (vbRed)", "&H000000FF&"),
            ("Verde (vbGreen)", "&H0000FF00&"),
            ("Azul (vbBlue)", "&H00FF0000&"),
            ("Amarillo (vbYellow)", "&H0000FFFF&"),
            ("Cian (vbCyan)", "&H00FFFF00&"),
            ("Magenta (vbMagenta)", "&H00FF00FF&"),
            ("Gris (vbGray)", "&H00808080&")
        };

        foreach (var (name, hex) in colors)
        {
            var mi = new MenuItem
            {
                Header = name,
                Icon = new Border
                {
                    Width = 14,
                    Height = 14,
                    Background = VbColorHelper.GetBrushFromHex(hex, VbColorModel.DefaultControlColor),
                    BorderBrush = Brushes.Gray,
                    BorderThickness = new Thickness(1)
                }
            };
            mi.Click += (_, _) =>
            {
                prop.CurrentValue = hex;
            };
            menu.Items.Add(mi);
        }

        menu.PlacementTarget = anchor;
        menu.IsOpen = true;
    }
}
