using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VB7x64.ProjectModel.Designers;
using VB7x64.ProjectModel.Execution;

using System.Windows.Threading;

namespace VB7x64.IDE.Execution;

/// <summary>
/// Proxy interactivo en vivo de un control de VB6 montado sobre un elemento visual WPF.
/// Permite leer y modificar propiedades en caliente (Text, Caption, Enabled, Visible, Left, Top, etc.)
/// y ejecutar métodos estándar (SetFocus, Clear, AddItem, RemoveItem).
/// </summary>
public class LiveControlProxy : ILiveControl
{
    private readonly FormDesignerControl _designerControl;
    private readonly FrameworkElement _visual;
    private readonly DispatcherTimer? _timer;
    private readonly Dictionary<string, VbValue> _dynamicProperties = new(StringComparer.OrdinalIgnoreCase);

    public string Name => _designerControl.Name;
    public string TypeName => _designerControl.TypeName;
    public FrameworkElement Visual => _visual;
    public FormDesignerControl DesignerControl => _designerControl;

    public LiveControlProxy(FormDesignerControl designerControl, FrameworkElement visual, DispatcherTimer? timer = null)
    {
        _designerControl = designerControl ?? throw new ArgumentNullException(nameof(designerControl));
        _visual = visual ?? throw new ArgumentNullException(nameof(visual));
        _timer = timer;
    }

    public VbValue GetProperty(string propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName)) return VbValue.Empty;

        return _visual.Dispatcher.Invoke(() =>
        {
            switch (propertyName.ToUpperInvariant())
            {
                case "NAME":
                    return VbValue.FromString(_designerControl.Name);

                case "INDEX":
                    return VbValue.FromLong(_designerControl.Index ?? 0);

                case "TEXT":
                case "CAPTION":
                case "VALUE":
                    return GetControlTextOrValue();

                case "ENABLED":
                    return VbValue.FromBoolean(_timer != null ? _timer.IsEnabled : _visual.IsEnabled);

                case "INTERVAL":
                    return VbValue.FromLong(_designerControl.Interval);

                case "VISIBLE":
                    return VbValue.FromBoolean(_visual.Visibility == Visibility.Visible);

                case "LEFT":
                    return VbValue.FromDouble(Canvas.GetLeft(_visual) * FormDesignModel.TwipsPerPixel);

                case "TOP":
                    return VbValue.FromDouble(Canvas.GetTop(_visual) * FormDesignModel.TwipsPerPixel);

                case "WIDTH":
                    return VbValue.FromDouble(_visual.Width * FormDesignModel.TwipsPerPixel);

                case "HEIGHT":
                    return VbValue.FromDouble(_visual.Height * FormDesignModel.TwipsPerPixel);

                case "TAG":
                    return _dynamicProperties.TryGetValue("Tag", out var tag) ? tag : VbValue.FromString(_visual.Tag?.ToString() ?? string.Empty);

                case "LISTCOUNT":
                    if (_visual is ListBox lb) return VbValue.FromLong(lb.Items.Count);
                    if (_visual is ComboBox cb) return VbValue.FromLong(cb.Items.Count);
                    return VbValue.FromLong(0);

                case "LISTINDEX":
                    if (_visual is ListBox lbIdx) return VbValue.FromLong(lbIdx.SelectedIndex);
                    if (_visual is ComboBox cbIdx) return VbValue.FromLong(cbIdx.SelectedIndex);
                    return VbValue.FromLong(-1);

                default:
                    if (_dynamicProperties.TryGetValue(propertyName, out var dynamicVal))
                    {
                        return dynamicVal;
                    }
                    var prop = _designerControl.CustomProperties.Find(p => p.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase));
                    return prop != null ? VbValue.FromString(prop.Value) : VbValue.Empty;
            }
        });
    }

    public void SetProperty(string propertyName, VbValue value)
    {
        if (string.IsNullOrWhiteSpace(propertyName)) return;

        _visual.Dispatcher.Invoke(() =>
        {
            switch (propertyName.ToUpperInvariant())
            {
                case "TEXT":
                case "CAPTION":
                    SetControlText(value.AsString());
                    break;

                case "VALUE":
                    SetControlValue(value);
                    break;

                case "ENABLED":
                    bool en = value.AsBoolean();
                    _visual.IsEnabled = en;
                    _designerControl.Enabled = en;
                    if (_timer != null)
                    {
                        _timer.IsEnabled = en;
                        if (en && _designerControl.Interval > 0)
                        {
                            if (!_timer.IsEnabled) _timer.Start();
                        }
                        else
                        {
                            _timer.Stop();
                        }
                    }
                    break;

                case "INTERVAL":
                    int ms = Math.Max(0, Math.Min(65535, (int)value.AsLong()));
                    _designerControl.Interval = ms;
                    if (_timer != null)
                    {
                        if (ms > 0)
                        {
                            _timer.Interval = TimeSpan.FromMilliseconds(ms);
                            if (_designerControl.Enabled)
                            {
                                _timer.Start();
                            }
                        }
                        else
                        {
                            _timer.Stop();
                        }
                    }
                    break;

                case "VISIBLE":
                    _visual.Visibility = value.AsBoolean() ? Visibility.Visible : Visibility.Hidden;
                    _designerControl.Visible = value.AsBoolean();
                    break;

                case "LEFT":
                    Canvas.SetLeft(_visual, value.AsDouble() / FormDesignModel.TwipsPerPixel);
                    _designerControl.Left = (int)Math.Round(value.AsDouble());
                    break;

                case "TOP":
                    Canvas.SetTop(_visual, value.AsDouble() / FormDesignModel.TwipsPerPixel);
                    _designerControl.Top = (int)Math.Round(value.AsDouble());
                    break;

                case "WIDTH":
                    _visual.Width = Math.Max(1, value.AsDouble() / FormDesignModel.TwipsPerPixel);
                    _designerControl.Width = (int)Math.Round(value.AsDouble());
                    break;

                case "HEIGHT":
                    _visual.Height = Math.Max(1, value.AsDouble() / FormDesignModel.TwipsPerPixel);
                    _designerControl.Height = (int)Math.Round(value.AsDouble());
                    break;

                case "BACKCOLOR":
                    ApplyBackground(value.AsLong());
                    break;

                case "FORECOLOR":
                    ApplyForeground(value.AsLong());
                    break;

                case "LISTINDEX":
                    int idx = (int)value.AsLong();
                    if (_visual is ListBox lb) lb.SelectedIndex = idx;
                    if (_visual is ComboBox cb) cb.SelectedIndex = idx;
                    break;

                default:
                    _dynamicProperties[propertyName] = value;
                    break;
            }
        });
    }

    public VbValue InvokeMethod(string methodName, VbValue[] args)
    {
        return _visual.Dispatcher.Invoke(() =>
        {
            switch (methodName.ToUpperInvariant())
            {
                case "SETFOCUS":
                    _visual.Focus();
                    return VbValue.Empty;

                case "CLEAR":
                    if (_visual is TextBox tb) tb.Clear();
                    else if (_visual is ListBox lb) lb.Items.Clear();
                    else if (_visual is ComboBox cb) cb.Items.Clear();
                    return VbValue.Empty;

                case "ADDITEM":
                    if (args.Length > 0)
                    {
                        string itemText = args[0].AsString();
                        if (_visual is ListBox lbAdd) lbAdd.Items.Add(itemText);
                        else if (_visual is ComboBox cbAdd) cbAdd.Items.Add(itemText);
                    }
                    return VbValue.Empty;

                case "REMOVEITEM":
                    if (args.Length > 0)
                    {
                        int removeIdx = (int)args[0].AsLong();
                        if (_visual is ListBox lbRem && removeIdx >= 0 && removeIdx < lbRem.Items.Count) lbRem.Items.RemoveAt(removeIdx);
                        else if (_visual is ComboBox cbRem && removeIdx >= 0 && removeIdx < cbRem.Items.Count) cbRem.Items.RemoveAt(removeIdx);
                    }
                    return VbValue.Empty;

                case "REFRESH":
                    _visual.UpdateLayout();
                    return VbValue.Empty;

                default:
                    return VbValue.Empty;
            }
        });
    }

    private VbValue GetControlTextOrValue()
    {
        if (_visual is TextBox tb) return VbValue.FromString(tb.Text);
        if (_visual is Button btn) return VbValue.FromString(btn.Content?.ToString() ?? string.Empty);
        if (_visual is TextBlock txt) return VbValue.FromString(txt.Text);
        if (_visual is CheckBox chk) return VbValue.FromLong(chk.IsChecked == true ? 1 : 0);
        if (_visual is RadioButton rad) return VbValue.FromBoolean(rad.IsChecked == true);
        if (_visual is ComboBox cb) return VbValue.FromString(cb.SelectedItem?.ToString() ?? cb.Text);
        if (_visual is ListBox lb) return VbValue.FromString(lb.SelectedItem?.ToString() ?? string.Empty);

        return VbValue.FromString(_designerControl.Text.Length > 0 ? _designerControl.Text : _designerControl.Caption);
    }

    private void SetControlText(string text)
    {
        _designerControl.Text = text;
        _designerControl.Caption = text;

        if (_visual is TextBox tb) tb.Text = text;
        else if (_visual is Button btn) btn.Content = text;
        else if (_visual is TextBlock txt) txt.Text = text;
        else if (_visual is CheckBox chk) chk.Content = text;
        else if (_visual is RadioButton rad) rad.Content = text;
        else if (_visual is GroupBox gb) gb.Header = text;
        else if (_visual is ComboBox cb) cb.Text = text;
    }

    private void SetControlValue(VbValue value)
    {
        if (_visual is CheckBox chk)
        {
            chk.IsChecked = value.AsLong() != 0;
        }
        else if (_visual is RadioButton rad)
        {
            rad.IsChecked = value.AsBoolean();
        }
        else
        {
            SetControlText(value.AsString());
        }
    }

    private void ApplyBackground(long colorRef)
    {
        var brush = VbColorToBrush(colorRef);
        if (_visual is Control ctrl) ctrl.Background = brush;
        else if (_visual is Panel panel) panel.Background = brush;
        else if (_visual is Border border) border.Background = brush;
    }

    private void ApplyForeground(long colorRef)
    {
        var brush = VbColorToBrush(colorRef);
        if (_visual is Control ctrl) ctrl.Foreground = brush;
        else if (_visual is TextBlock tb) tb.Foreground = brush;
    }

    private static Brush VbColorToBrush(long colorRef)
    {
        // Si es color de sistema VB6 (&H800000xx) o RGB normal (0x00BBGGRR)
        if ((colorRef & 0x80000000) != 0)
        {
            int sysIdx = (int)(colorRef & 0xFF);
            return sysIdx switch
            {
                0x0F => SystemColors.ControlBrush,
                0x12 => SystemColors.ControlTextBrush,
                0x05 => SystemColors.WindowBrush,
                0x08 => SystemColors.WindowTextBrush,
                _ => SystemColors.ControlBrush
            };
        }

        byte r = (byte)(colorRef & 0xFF);
        byte g = (byte)((colorRef >> 8) & 0xFF);
        byte b = (byte)((colorRef >> 16) & 0xFF);
        return new SolidColorBrush(Color.FromRgb(r, g, b));
    }
}
