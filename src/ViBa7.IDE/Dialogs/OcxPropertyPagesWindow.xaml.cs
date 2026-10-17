using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ViBa7.IDE.Designer;
using ViBa7.ProjectModel.Designers;

namespace ViBa7.IDE.Dialogs;

public class OcxPropertyGridEntry : INotifyPropertyChanged
{
    private string _value = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Value
    {
        get => _value;
        set
        {
            if (_value != value)
            {
                _value = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            }
        }
    }

    public string Category { get; init; } = "General";

    public event PropertyChangedEventHandler? PropertyChanged;
}

public partial class OcxPropertyPagesWindow : Window
{
    private readonly FormDesignerControl _control;
    private readonly FormDesignModel? _formModel;
    private readonly Action? _onModelChanged;

    private readonly ObservableCollection<OcxPropertyGridEntry> _allProperties = new();
    private readonly List<OcxPropertyGridEntry> _masterPropertyList = new();
    private bool _isInitializing = true;

    public OcxPropertyPagesWindow(FormDesignerControl control, FormDesignModel? formModel, Action? onModelChanged = null)
    {
        _control = control ?? throw new ArgumentNullException(nameof(control));
        _formModel = formModel;
        _onModelChanged = onModelChanged;

        InitializeComponent();
        Title = $"Páginas de Propiedades - {_control.Name} ({_control.TypeName})";

        LoadControlData();
        _isInitializing = false;
        UpdateColorPreview();
        UpdateFontPreview();
    }

    private void LoadControlData()
    {
        TxtControlName.Text = _control.Name;
        TxtControlType.Text = $"({_control.TypeName})";

        if (_control.IsUnresolved)
        {
            BadgeUnresolved.Visibility = Visibility.Visible;
            TxtControlSummary.Text = string.IsNullOrWhiteSpace(_control.UnresolvedErrorMessage)
                ? "⚠️ Control no registrado o faltante en Windows. Las propiedades se preservan íntegras."
                : $"⚠️ {_control.UnresolvedErrorMessage} (Propiedades preservadas íntegras)";
        }
        else
        {
            TxtControlSummary.Text = "Páginas de propiedades del fabricante / contenedor ActiveX para Visual Basic 6.0";
        }

        // 1. General
        TxtCaption.Text = _control.Caption ?? string.Empty;
        TxtText.Text = _control.Text ?? string.Empty;
        TxtIndex.Text = _control.Index?.ToString() ?? string.Empty;
        CmbBorderStyle.SelectedIndex = _control.BorderStyle.GetValueOrDefault(0) == 1 ? 1 : 0;
        CmbAlignment.SelectedIndex = Math.Clamp(_control.Alignment.GetValueOrDefault(0), 0, 2);
        TxtToolTipText.Text = _control.GetCustomProperty("ToolTipText") ?? string.Empty;
        TxtTag.Text = _control.GetCustomProperty("Tag") ?? string.Empty;
        ChkEnabled.IsChecked = _control.Enabled != false;
        ChkVisible.IsChecked = _control.Visible != false;
        ChkTabStop.IsChecked = _control.TabStop != false;

        // 2. Colores
        TxtBackColor.Text = _control.BackColorHex ?? "&H8000000F&";
        TxtForeColor.Text = _control.ForeColorHex ?? "&H80000008&";

        // 3. Disposición
        TxtLeftTwips.Text = _control.Left.ToString();
        TxtTopTwips.Text = _control.Top.ToString();
        TxtWidthTwips.Text = _control.Width.ToString();
        TxtHeightTwips.Text = _control.Height.ToString();

        TxtExtentX.Text = _control.GetCustomProperty("_ExtentX") ?? string.Empty;
        TxtExtentY.Text = _control.GetCustomProperty("_ExtentY") ?? string.Empty;

        UpdatePixelsFromTwips();

        // 4. Fuente
        PopulateFontList();
        var currentFont = _control.Font ?? new FormFontDescription();
        SelectFontInCombo(currentFont.Name);
        CmbFontSize.Text = currentFont.Size.ToString(CultureInfo.InvariantCulture);
        ChkFontBold.IsChecked = currentFont.IsBold;
        ChkFontItalic.IsChecked = currentFont.Italic;
        ChkFontUnderline.IsChecked = currentFont.Underline;
        ChkFontStrikethrough.IsChecked = currentFont.Strikethrough;

        // 5. Todas las propiedades
        PopulateAllProperties();
    }

    private void PopulateFontList()
    {
        var commonFonts = new[]
        {
            "Tahoma", "MS Sans Serif", "Segoe UI", "Arial", "Times New Roman",
            "Courier New", "Verdana", "Comic Sans MS", "Georgia", "Trebuchet MS", "Impact"
        };

        var allFontNames = new SortedSet<string>(commonFonts);
        foreach (var fontFam in Fonts.SystemFontFamilies)
        {
            allFontNames.Add(fontFam.Source);
        }

        CmbFontName.ItemsSource = allFontNames.ToList();

        var sizes = new[] { "8", "8.25", "9", "9.75", "10", "11", "12", "14", "16", "18", "20", "24", "28", "36" };
        CmbFontSize.ItemsSource = sizes;
    }

    private void SelectFontInCombo(string fontName)
    {
        if (CmbFontName.ItemsSource is IEnumerable<string> items)
        {
            foreach (var item in items)
            {
                if (string.Equals(item, fontName, StringComparison.OrdinalIgnoreCase))
                {
                    CmbFontName.SelectedItem = item;
                    return;
                }
            }
        }
        CmbFontName.Text = fontName;
    }

    private void PopulateAllProperties()
    {
        _masterPropertyList.Clear();
        _allProperties.Clear();

        var handled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddProp(string name, string? val, string category)
        {
            if (handled.Contains(name)) return;
            handled.Add(name);
            var entry = new OcxPropertyGridEntry
            {
                Name = name,
                Value = val ?? string.Empty,
                Category = category
            };
            _masterPropertyList.Add(entry);
        }

        // Estándar
        AddProp("Name", _control.Name, "Estándar");
        AddProp("Caption", _control.Caption, "Estándar");
        AddProp("Text", _control.Text, "Estándar");
        AddProp("Left", _control.Left.ToString(), "Posición");
        AddProp("Top", _control.Top.ToString(), "Posición");
        AddProp("Width", _control.Width.ToString(), "Posición");
        AddProp("Height", _control.Height.ToString(), "Posición");
        AddProp("BackColor", _control.BackColorHex, "Apariencia");
        AddProp("ForeColor", _control.ForeColorHex, "Apariencia");
        AddProp("Enabled", _control.Enabled.ToString(), "Comportamiento");
        AddProp("Visible", _control.Visible.ToString(), "Comportamiento");
        AddProp("TabStop", _control.TabStop.ToString(), "Comportamiento");
        AddProp("Index", _control.Index?.ToString(), "General");
        AddProp("BorderStyle", _control.BorderStyle?.ToString(), "Apariencia");
        AddProp("Alignment", _control.Alignment?.ToString(), "Apariencia");
        AddProp("ToolTipText", _control.GetCustomProperty("ToolTipText"), "General");
        AddProp("Tag", _control.GetCustomProperty("Tag"), "General");

        // CustomProperties del control (OCX / UserControl)
        foreach (var cust in _control.CustomProperties)
        {
            if (cust.IsBeginProperty) continue;
            AddProp(cust.Name, cust.Value, "Custom / OCX");
        }

        // Miembros registrados en la biblioteca de tipos (TypeLib de OCX / Catálogo dinámico)
        var members = ViBa7.ProjectModel.IntelliSense.VbControlMemberCatalog.GetMembersForControl(_control.TypeName);
        foreach (var m in members)
        {
            if (m.Kind == ViBa7.ProjectModel.IntelliSense.VbSymbolKind.Property)
            {
                string? curVal = _control.GetCustomProperty(m.Name);
                AddProp(m.Name, curVal ?? string.Empty, "ActiveX / TypeLib");
            }
        }

        // Propiedades de UserControls del proyecto
        if (DesignerPropertyFactory.UserControlPropertiesResolver != null)
        {
            var ucProps = DesignerPropertyFactory.UserControlPropertiesResolver(_control.TypeName);
            if (ucProps != null)
            {
                foreach (var p in ucProps)
                {
                    string? curVal = _control.GetCustomProperty(p.Name);
                    AddProp(p.Name, curVal ?? p.DefaultValue ?? string.Empty, "UserControl");
                }
            }
        }

        foreach (var item in _masterPropertyList)
        {
            _allProperties.Add(item);
        }

        DgProperties.ItemsSource = _allProperties;
    }

    private void TxtPropertySearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        string filter = TxtPropertySearch.Text?.Trim() ?? string.Empty;
        _allProperties.Clear();

        foreach (var item in _masterPropertyList)
        {
            if (string.IsNullOrEmpty(filter) ||
                item.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                item.Value.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                item.Category.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                _allProperties.Add(item);
            }
        }
    }

    private void BtnAddProperty_Click(object sender, RoutedEventArgs e)
    {
        string propName = Microsoft.VisualBasic.Interaction.InputBox("Nombre de la nueva propiedad:", "Agregar Propiedad", "CustomProp1");
        if (string.IsNullOrWhiteSpace(propName)) return;

        propName = propName.Trim();
        if (_masterPropertyList.Any(p => string.Equals(p.Name, propName, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show($"La propiedad '{propName}' ya existe.", "Propiedad Duplicada", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string val = Microsoft.VisualBasic.Interaction.InputBox($"Valor para '{propName}':", "Valor de Propiedad", "0");
        var newEntry = new OcxPropertyGridEntry
        {
            Name = propName,
            Value = val,
            Category = "Custom / OCX"
        };
        _masterPropertyList.Add(newEntry);
        _allProperties.Add(newEntry);
        DgProperties.SelectedItem = newEntry;
    }

    private void BtnRemoveProperty_Click(object sender, RoutedEventArgs e)
    {
        if (DgProperties.SelectedItem is OcxPropertyGridEntry entry)
        {
            if (entry.Category == "Estándar" || entry.Category == "Posición")
            {
                MessageBox.Show($"La propiedad '{entry.Name}' es estándar del control y no puede eliminarse.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _masterPropertyList.Remove(entry);
            _allProperties.Remove(entry);
        }
    }

    private void OnFieldValueChanged(object sender, TextChangedEventArgs e)
    {
        if (_isInitializing) return;
        TxtStatus.Text = "Cambios pendientes de aplicar.";
    }

    private void OnFieldSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;
        TxtStatus.Text = "Cambios pendientes de aplicar.";
    }

    private void OnFieldCheckChanged(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        TxtStatus.Text = "Cambios pendientes de aplicar.";
    }

    #region Colores
    private void TxtBackColor_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateColorPreview();
        if (!_isInitializing) TxtStatus.Text = "Cambios pendientes de aplicar.";
    }

    private void TxtForeColor_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateColorPreview();
        if (!_isInitializing) TxtStatus.Text = "Cambios pendientes de aplicar.";
    }

    private void UpdateColorPreview()
    {
        try
        {
            var bgBrush = VbColorHelper.GetBrushFromHex(TxtBackColor.Text, VbColorModel.DefaultControlColor);
            ColorBoxBack.Background = bgBrush;
            ColorSampleBorder.Background = bgBrush;

            var fgBrush = VbColorHelper.ToBrush(TxtForeColor.Text, Brushes.Black);
            ColorBoxFore.Background = fgBrush;
            ColorSampleText.Foreground = fgBrush;
        }
        catch
        {
            // Ignorar formato temporal incompleto mientras el usuario escribe
        }
    }

    private void BtnEyedropperBack_Click(object sender, RoutedEventArgs e)
    {
        ScreenColorDropper.StartColorPick(color =>
        {
            TxtBackColor.Text = VbColorHelper.ToVbColorHex(color);
        });
    }

    private void BtnEyedropperFore_Click(object sender, RoutedEventArgs e)
    {
        ScreenColorDropper.StartColorPick(color =>
        {
            TxtForeColor.Text = VbColorHelper.ToVbColorHex(color);
        });
    }

    private void BtnPickBackColor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
        {
            ShowColorPaletteMenu(btn, hex => TxtBackColor.Text = hex);
        }
    }

    private void BtnPickForeColor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
        {
            ShowColorPaletteMenu(btn, hex => TxtForeColor.Text = hex);
        }
    }

    private void ShowColorPaletteMenu(Button anchor, Action<string> onColorSelected)
    {
        var menu = new ContextMenu();
        var colors = new (string Name, string Hex)[]
        {
            ("ButtonFace (&H8000000F&)", "&H8000000F&"),
            ("Window Blanco (&H80000005&)", "&H80000005&"),
            ("WindowText Negro (&H80000008&)", "&H80000008&"),
            ("ButtonHighlight (&H80000014&)", "&H80000014&"),
            ("ButtonShadow (&H80000010&)", "&H80000010&"),
            ("Highlight Azul (&H8000000D&)", "&H8000000D&"),
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
            mi.Click += (_, _) => onColorSelected(hex);
            menu.Items.Add(mi);
        }

        menu.PlacementTarget = anchor;
        menu.IsOpen = true;
    }
    #endregion

    #region Disposición / Medidas
    private void TxtLeftTwips_TextChanged(object sender, TextChangedEventArgs e) => UpdatePixelsFromTwips();
    private void TxtTopTwips_TextChanged(object sender, TextChangedEventArgs e) => UpdatePixelsFromTwips();
    private void TxtWidthTwips_TextChanged(object sender, TextChangedEventArgs e) => UpdatePixelsFromTwips();
    private void TxtHeightTwips_TextChanged(object sender, TextChangedEventArgs e) => UpdatePixelsFromTwips();

    private void UpdatePixelsFromTwips()
    {
        if (int.TryParse(TxtLeftTwips?.Text, out int l))
            TxtLeftPixels.Text = Math.Round(l / FormDesignerControl.TwipsPerPixel, 1).ToString(CultureInfo.InvariantCulture);
        if (int.TryParse(TxtTopTwips?.Text, out int t))
            TxtTopPixels.Text = Math.Round(t / FormDesignerControl.TwipsPerPixel, 1).ToString(CultureInfo.InvariantCulture);
        if (int.TryParse(TxtWidthTwips?.Text, out int w))
            TxtWidthPixels.Text = Math.Round(w / FormDesignerControl.TwipsPerPixel, 1).ToString(CultureInfo.InvariantCulture);
        if (int.TryParse(TxtHeightTwips?.Text, out int h))
            TxtHeightPixels.Text = Math.Round(h / FormDesignerControl.TwipsPerPixel, 1).ToString(CultureInfo.InvariantCulture);
    }
    #endregion

    #region Fuente
    private void OnFontChanged(object sender, RoutedEventArgs e)
    {
        UpdateFontPreview();
        if (!_isInitializing) TxtStatus.Text = "Cambios pendientes de aplicar.";
    }

    private void CmbFontSize_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateFontPreview();
        if (!_isInitializing) TxtStatus.Text = "Cambios pendientes de aplicar.";
    }

    private void UpdateFontPreview()
    {
        if (TxtFontSample == null) return;

        string fontName = CmbFontName.SelectedItem as string ?? CmbFontName.Text;
        if (!string.IsNullOrWhiteSpace(fontName))
        {
            TxtFontSample.FontFamily = new FontFamily(fontName);
        }

        if (double.TryParse(CmbFontSize.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double sz) && sz > 0)
        {
            // Tamaño en puntos a píxeles WPF: pt * 96 / 72 = pt * 4 / 3
            TxtFontSample.FontSize = Math.Clamp(sz * (96.0 / 72.0), 6.0, 72.0);
        }

        TxtFontSample.FontWeight = ChkFontBold.IsChecked == true ? FontWeights.Bold : FontWeights.Normal;
        TxtFontSample.FontStyle = ChkFontItalic.IsChecked == true ? FontStyles.Italic : FontStyles.Normal;

        var textDecorations = new TextDecorationCollection();
        if (ChkFontUnderline.IsChecked == true) textDecorations.Add(TextDecorations.Underline);
        if (ChkFontStrikethrough.IsChecked == true) textDecorations.Add(TextDecorations.Strikethrough);
        TxtFontSample.TextDecorations = textDecorations;
    }
    #endregion

    #region Aplicación de Cambios
    public bool ApplyChanges()
    {
        try
        {
            // 1. General
            _control.Caption = TxtCaption.Text;
            _control.Text = TxtText.Text;

            if (int.TryParse(TxtIndex.Text?.Trim(), out int idx) && idx >= 0)
            {
                _control.Index = idx;
            }
            else if (string.IsNullOrWhiteSpace(TxtIndex.Text))
            {
                _control.Index = null;
            }

            _control.BorderStyle = CmbBorderStyle.SelectedIndex == 1 ? 1 : 0;
            _control.Alignment = CmbAlignment.SelectedIndex;
            if (!string.IsNullOrWhiteSpace(TxtToolTipText.Text))
                _control.SetCustomProperty("ToolTipText", TxtToolTipText.Text);
            if (!string.IsNullOrWhiteSpace(TxtTag.Text))
                _control.SetCustomProperty("Tag", TxtTag.Text);
            _control.Enabled = ChkEnabled.IsChecked == true;
            _control.Visible = ChkVisible.IsChecked == true;
            _control.TabStop = ChkTabStop.IsChecked == true;

            // 2. Colores
            _control.BackColorHex = TxtBackColor.Text;
            _control.ForeColorHex = TxtForeColor.Text;
            DesignerPropertyFactory.SynchronizeStandardProperties(_control, "BackColor", TxtBackColor.Text);
            DesignerPropertyFactory.SynchronizeStandardProperties(_control, "ForeColor", TxtForeColor.Text);

            // 3. Disposición
            if (int.TryParse(TxtLeftTwips.Text, out int left)) _control.Left = left;
            if (int.TryParse(TxtTopTwips.Text, out int top)) _control.Top = top;
            if (int.TryParse(TxtWidthTwips.Text, out int width)) _control.Width = width;
            if (int.TryParse(TxtHeightTwips.Text, out int height)) _control.Height = height;

            if (!string.IsNullOrWhiteSpace(TxtExtentX.Text)) _control.SetCustomProperty("_ExtentX", TxtExtentX.Text);
            if (!string.IsNullOrWhiteSpace(TxtExtentY.Text)) _control.SetCustomProperty("_ExtentY", TxtExtentY.Text);

            // 4. Fuente
            var font = _control.Font ?? new FormFontDescription();
            string fontName = CmbFontName.SelectedItem as string ?? CmbFontName.Text;
            if (!string.IsNullOrWhiteSpace(fontName)) font.Name = fontName;

            if (double.TryParse(CmbFontSize.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double fSize) && fSize > 0)
            {
                font.Size = fSize;
            }
            font.IsBold = ChkFontBold.IsChecked == true;
            font.Italic = ChkFontItalic.IsChecked == true;
            font.Underline = ChkFontUnderline.IsChecked == true;
            font.Strikethrough = ChkFontStrikethrough.IsChecked == true;
            _control.Font = font;

            // 5. Propiedades del DataGrid (Custom y todas las demás)
            foreach (var prop in _masterPropertyList)
            {
                if (prop.Category == "Custom / OCX")
                {
                    _control.SetCustomProperty(prop.Name, prop.Value);
                    DesignerPropertyFactory.SynchronizeStandardProperties(_control, prop.Name, prop.Value);
                }
            }

            _onModelChanged?.Invoke();
            TxtStatus.Text = $"Propiedades aplicadas exitosamente a las {DateTime.Now:HH:mm:ss}.";
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al aplicar propiedades: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            TxtStatus.Text = "Error al aplicar cambios.";
            return false;
        }
    }

    private void BtnApply_Click(object sender, RoutedEventArgs e)
    {
        ApplyChanges();
    }

    private void BtnOK_Click(object sender, RoutedEventArgs e)
    {
        if (ApplyChanges())
        {
            DialogResult = true;
            Close();
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
    #endregion
}
