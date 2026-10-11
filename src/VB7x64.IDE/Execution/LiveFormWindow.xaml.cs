using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using VB7x64.IDE.Designer;
using VB7x64.ProjectModel.Designers;
using VB7x64.ProjectModel.Execution;

namespace VB7x64.IDE.Execution;

/// <summary>
/// Ventana anfitriona para la ejecución interactiva en caliente (Hot-Run) de formularios VB6.
/// Instancia controles WPF dinámicos, enlaza eventos en vivo con el motor de ejecución
/// y mantiene sincronizada la interfaz visual con las sentencias del script.
/// </summary>
public partial class LiveFormWindow : Window
{
    private readonly FormDesignModel _model;
    private readonly LiveExecutionHost _host;
    private readonly LiveFormProxy _formProxy;
    private readonly List<DispatcherTimer> _activeTimers = new();

    public LiveFormProxy FormProxy => _formProxy;

    public LiveFormWindow(FormDesignModel model, LiveExecutionHost host, string? sourceCode = null)
    {
        InitializeComponent();

        _model = model ?? throw new ArgumentNullException(nameof(model));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _formProxy = new LiveFormProxy(_model, this);

        Title = string.IsNullOrEmpty(_model.Caption) ? _model.Name : _model.Caption;
        Width = Math.Max(200, _model.ClientWidthPixels);
        Height = Math.Max(150, _model.ClientHeightPixels);

        if (!string.IsNullOrWhiteSpace(_model.BackColorHex))
        {
            var brush = VbColorHelper.ToBrush(_model.BackColorHex, SystemColors.ControlBrush);
            Background = brush;
            FormCanvas.Background = brush;
        }

        if (sourceCode != null)
        {
            _host.LoadSourceCode(sourceCode, _model.Name);
        }

        BuildControls();
        WireEvents();
    }

    private void BuildControls()
    {
        // 1. Registrar el formulario y la variable Me en el contexto
        _host.Context.ActiveForms[_model.Name] = _formProxy;
        _host.Context.SetVariable(_model.Name, VbValue.FromObject(_formProxy));
        _host.Context.SetVariable("Me", VbValue.FromObject(_formProxy));

        // 2. Construir menús de formulario si existen
        BuildMenus();

        // 3. Normalizar jerarquía del modelo para asegurar que ningún control hijo permanezca en la raíz
        NormalizeModelHierarchy();

        // 4. Construir controles jerárquicamente a partir de los controles de primer nivel
        foreach (var ctrl in _model.Children)
        {
            if (ctrl.TypeName.Equals("VB.Menu", StringComparison.OrdinalIgnoreCase))
            {
                continue; // Ya procesado en BuildMenus
            }

            if (ctrl.ParentControl != null)
            {
                continue; // Los controles anidados solo se construyen dentro de su contenedor padre
            }

            BuildControlRecursive(ctrl, FormCanvas);
        }
    }

    private void NormalizeModelHierarchy()
    {
        var allControls = _model.GetAllControls();
        foreach (var ctrl in allControls)
        {
            if (ctrl.ParentControl != null)
            {
                _model.Children.Remove(ctrl);
                if (!ctrl.ParentControl.Children.Contains(ctrl))
                {
                    ctrl.ParentControl.Children.Add(ctrl);
                }
            }
        }
    }

    private void BuildControlRecursive(FormDesignerControl ctrl, Canvas parentCanvas)
    {
        var visual = CreateLiveControlVisual(ctrl);
        if (visual != null)
        {
            Canvas.SetLeft(visual, ctrl.LeftPixels);
            Canvas.SetTop(visual, ctrl.TopPixels);
            visual.Width = ctrl.WidthPixels;
            visual.Height = ctrl.HeightPixels;

            parentCanvas.Children.Add(visual);

            var proxy = new LiveControlProxy(ctrl, visual);
            _formProxy.RegisterControl(proxy);
            _host.Context.SetVariable(ctrl.Name, VbValue.FromObject(proxy));

            // Si es un contenedor, obtener su lienzo interno para añadir sus hijos
            Canvas? innerCanvas = GetContainerInnerCanvas(visual);
            if (innerCanvas != null)
            {
                foreach (var child in ctrl.Children)
                {
                    if (child.TypeName.Equals("VB.Menu", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    BuildControlRecursive(child, innerCanvas);
                }
            }
        }
        else
        {
            // Para controles sin representación visual en runtime (ej. Timer)
            var dummyVisual = new FrameworkElement { Name = ctrl.Name };
            var proxy = new LiveControlProxy(ctrl, dummyVisual);
            _formProxy.RegisterControl(proxy);
            _host.Context.SetVariable(ctrl.Name, VbValue.FromObject(proxy));
        }
    }

    private static Canvas? GetContainerInnerCanvas(FrameworkElement visual)
    {
        if (visual is GroupBox gb && gb.Content is Canvas c1)
        {
            return c1;
        }
        if (visual is Border b && b.Child is Canvas c2)
        {
            return c2;
        }
        if (visual is Canvas c3)
        {
            return c3;
        }
        return null;
    }

    private void BuildMenus()
    {
        var menuControls = _model.GetAllControls()
            .FindAll(c => c.TypeName.Equals("VB.Menu", StringComparison.OrdinalIgnoreCase));

        if (menuControls.Count == 0) return;

        FormMenuBar.Visibility = Visibility.Visible;
        FormMenuBar.Items.Clear();

        var topLevelMenus = menuControls.FindAll(c => c.ParentControl == null);
        foreach (var topMenu in topLevelMenus)
        {
            var menuItem = CreateMenuItemHierarchy(topMenu, menuControls);
            FormMenuBar.Items.Add(menuItem);
        }
    }

    private MenuItem CreateMenuItemHierarchy(FormDesignerControl menuCtrl, List<FormDesignerControl> allMenus)
    {
        var item = new MenuItem
        {
            Header = menuCtrl.Caption,
            IsEnabled = menuCtrl.Enabled,
            IsChecked = menuCtrl.Checked
        };

        if (!string.IsNullOrEmpty(menuCtrl.Shortcut))
        {
            item.InputGestureText = menuCtrl.Shortcut;
        }

        string ctrlName = menuCtrl.Name;
        item.Click += (s, e) => _host.InvokeProcedure($"{ctrlName}_Click", _model.Name);

        var children = allMenus.FindAll(c => c.ParentControl == menuCtrl);
        foreach (var child in children)
        {
            item.Items.Add(CreateMenuItemHierarchy(child, allMenus));
        }

        return item;
    }

    private FrameworkElement? CreateLiveControlVisual(FormDesignerControl ctrl)
    {
        string normType = ctrl.TypeName.ToUpperInvariant();
        if (normType.StartsWith("VB.", StringComparison.OrdinalIgnoreCase))
        {
            normType = normType[3..];
        }

        string ctrlName = ctrl.Name;

        switch (normType)
        {
            case "COMMANDBUTTON":
                var btn = new Button
                {
                    Content = string.IsNullOrEmpty(ctrl.Caption) ? ctrl.Name : ctrl.Caption,
                    IsEnabled = ctrl.Enabled,
                    Visibility = ctrl.Visible ? Visibility.Visible : Visibility.Hidden,
                    Background = VbColorHelper.ToBrush(ctrl.BackColorHex, SystemColors.ControlBrush),
                    Foreground = VbColorHelper.ToBrush(ctrl.ForeColorHex, SystemColors.ControlTextBrush)
                };
                btn.Click += (s, e) => _host.InvokeProcedure($"{ctrlName}_Click", _model.Name);
                return btn;

            case "TEXTBOX":
                var tb = new TextBox
                {
                    Text = ctrl.Text,
                    IsEnabled = ctrl.Enabled,
                    Visibility = ctrl.Visible ? Visibility.Visible : Visibility.Hidden,
                    Background = VbColorHelper.ToBrush(ctrl.BackColorHex, SystemColors.WindowBrush),
                    Foreground = VbColorHelper.ToBrush(ctrl.ForeColorHex, SystemColors.WindowTextBrush)
                };
                if (ctrl.MultiLine == true)
                {
                    tb.AcceptsReturn = true;
                    tb.TextWrapping = TextWrapping.Wrap;
                    tb.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                }
                tb.TextChanged += (s, e) => _host.InvokeProcedure($"{ctrlName}_Change", _model.Name);
                return tb;

            case "LABEL":
                var lbl = new TextBlock
                {
                    Text = string.IsNullOrEmpty(ctrl.Caption) ? ctrl.Name : ctrl.Caption,
                    IsEnabled = ctrl.Enabled,
                    Visibility = ctrl.Visible ? Visibility.Visible : Visibility.Hidden,
                    Foreground = VbColorHelper.ToBrush(ctrl.ForeColorHex, SystemColors.ControlTextBrush),
                    VerticalAlignment = VerticalAlignment.Center
                };
                lbl.MouseDown += (s, e) => _host.InvokeProcedure($"{ctrlName}_Click", _model.Name);
                return lbl;

            case "CHECKBOX":
                var chk = new CheckBox
                {
                    Content = string.IsNullOrEmpty(ctrl.Caption) ? ctrl.Name : ctrl.Caption,
                    IsEnabled = ctrl.Enabled,
                    Visibility = ctrl.Visible ? Visibility.Visible : Visibility.Hidden,
                    Foreground = VbColorHelper.ToBrush(ctrl.ForeColorHex, SystemColors.ControlTextBrush)
                };
                chk.Click += (s, e) => _host.InvokeProcedure($"{ctrlName}_Click", _model.Name);
                return chk;

            case "OPTIONBUTTON":
                var rad = new RadioButton
                {
                    Content = string.IsNullOrEmpty(ctrl.Caption) ? ctrl.Name : ctrl.Caption,
                    IsEnabled = ctrl.Enabled,
                    Visibility = ctrl.Visible ? Visibility.Visible : Visibility.Hidden,
                    Foreground = VbColorHelper.ToBrush(ctrl.ForeColorHex, SystemColors.ControlTextBrush)
                };
                rad.Click += (s, e) => _host.InvokeProcedure($"{ctrlName}_Click", _model.Name);
                return rad;

            case "LISTBOX":
                var lb = new ListBox
                {
                    IsEnabled = ctrl.Enabled,
                    Visibility = ctrl.Visible ? Visibility.Visible : Visibility.Hidden
                };
                lb.SelectionChanged += (s, e) => _host.InvokeProcedure($"{ctrlName}_Click", _model.Name);
                lb.MouseDoubleClick += (s, e) => _host.InvokeProcedure($"{ctrlName}_DblClick", _model.Name);
                return lb;

            case "COMBOBOX":
                var cb = new ComboBox
                {
                    IsEnabled = ctrl.Enabled,
                    Visibility = ctrl.Visible ? Visibility.Visible : Visibility.Hidden,
                    IsEditable = true
                };
                cb.SelectionChanged += (s, e) => _host.InvokeProcedure($"{ctrlName}_Click", _model.Name);
                return cb;

            case "FRAME":
                var gb = new GroupBox
                {
                    Header = string.IsNullOrEmpty(ctrl.Caption) ? ctrl.Name : ctrl.Caption,
                    IsEnabled = ctrl.Enabled,
                    Visibility = ctrl.Visible ? Visibility.Visible : Visibility.Hidden,
                    BorderBrush = new SolidColorBrush(Color.FromRgb(208, 206, 191)),
                    BorderThickness = new Thickness(1),
                    Background = VbColorHelper.ToBrush(ctrl.BackColorHex, Brushes.Transparent),
                    Padding = new Thickness(2)
                };
                var frameCanvas = new Canvas
                {
                    ClipToBounds = true,
                    Background = Brushes.Transparent
                };
                gb.Content = frameCanvas;
                return gb;

            case "PICTUREBOX":
                var picBorder = new Border
                {
                    Background = VbColorHelper.ToBrush(ctrl.BackColorHex, Brushes.White),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(128, 128, 128)),
                    BorderThickness = new Thickness(ctrl.BorderStyle == 0 ? 0 : 1),
                    ClipToBounds = true
                };
                var picCanvas = new Canvas
                {
                    ClipToBounds = true,
                    Background = Brushes.Transparent
                };
                picBorder.Child = picCanvas;
                picBorder.MouseDown += (s, e) => _host.InvokeProcedure($"{ctrlName}_Click", _model.Name);
                return picBorder;

            case "TIMER":
                int intervalMs = 1000;
                var intervalProp = ctrl.CustomProperties.Find(p => p.Name.Equals("Interval", StringComparison.OrdinalIgnoreCase));
                if (intervalProp != null && int.TryParse(intervalProp.Value, out int iv) && iv > 0)
                {
                    intervalMs = iv;
                }

                var timer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(intervalMs),
                    IsEnabled = ctrl.Enabled
                };
                timer.Tick += (s, e) => _host.InvokeProcedure($"{ctrlName}_Timer", _model.Name);
                _activeTimers.Add(timer);
                timer.Start();

                // Un Timer no es visible en tiempo de ejecución en VB6
                return null;

            default:
                // Para otros controles gráficos (Shape, Image, PictureBox, etc.)
                return ControlVisualRenderer.CreateVisual(ctrl);
        }
    }

    private void WireEvents()
    {
        Loaded += (s, e) =>
        {
            _host.InvokeProcedure("Form_Load", _model.Name);
        };

        FormCanvas.MouseDown += (s, e) =>
        {
            _host.InvokeProcedure("Form_Click", _model.Name);
        };

        Closing += (s, e) =>
        {
            _host.InvokeProcedure("Form_Unload", _model.Name, VbValue.FromLong(0));
        };

        Closed += (s, e) =>
        {
            foreach (var timer in _activeTimers)
            {
                timer.Stop();
            }
            _activeTimers.Clear();

            _host.Context.ActiveForms.Remove(_model.Name);
            _host.Context.State = ExecutionState.Stopped;
        };
    }
}
