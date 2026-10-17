using System;
using System.Collections.Generic;
using System.Windows;
using ViBa7.ProjectModel.Designers;
using ViBa7.ProjectModel.Execution;

namespace ViBa7.IDE.Execution;

/// <summary>
/// Proxy interactivo en caliente de un formulario VB6 (Form) en ejecución en vivo.
/// Implementa ILiveForm permitiendo acceso a la colección de controles hijos y propiedades de ventana.
/// </summary>
public class LiveFormProxy : ILiveForm
{
    private readonly FormDesignModel _model;
    private readonly Window _window;
    private readonly Dictionary<string, ILiveControl> _controls = new(StringComparer.OrdinalIgnoreCase);

    public string Name => _model.Name;
    public string TypeName => "Form";
    public IReadOnlyDictionary<string, ILiveControl> Controls => _controls;

    public LiveFormProxy(FormDesignModel model, Window window)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _window = window ?? throw new ArgumentNullException(nameof(window));
    }

    public void RegisterControl(ILiveControl control, int? index = null)
    {
        if (index.HasValue)
        {
            if (!_controls.TryGetValue(control.Name, out var existing) || existing is not LiveControlArray arr)
            {
                arr = new LiveControlArray(control.Name);
                if (existing != null && existing is not LiveControlArray)
                {
                    arr.Add(0, existing);
                }
                _controls[control.Name] = arr;
            }
            arr.Add(index.Value, control);
            _controls[$"{control.Name}({index.Value})"] = control;
        }
        else
        {
            _controls[control.Name] = control;
        }
    }

    public ILiveControl? FindControl(string name)
    {
        if (_controls.TryGetValue(name, out var ctrl)) return ctrl;

        int paren = name.IndexOf('(');
        if (paren > 0 && name.EndsWith(')'))
        {
            string baseName = name[..paren].Trim();
            string idxStr = name[(paren + 1)..^1].Trim();
            if (int.TryParse(idxStr, out int idx) &&
                _controls.TryGetValue(baseName, out var baseCtrl) &&
                baseCtrl is ILiveControlArray arr)
            {
                return arr.GetItem(idx);
            }
        }

        return null;
    }

    public VbValue GetProperty(string propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName)) return VbValue.Empty;

        return _window.Dispatcher.Invoke(() =>
        {
            switch (propertyName.ToUpperInvariant())
            {
                case "NAME":
                    return VbValue.FromString(_model.Name);

                case "CAPTION":
                case "TITLE":
                    return VbValue.FromString(_window.Title);

                case "LEFT":
                    return VbValue.FromDouble(_window.Left * FormDesignModel.TwipsPerPixel);

                case "TOP":
                    return VbValue.FromDouble(_window.Top * FormDesignModel.TwipsPerPixel);

                case "WIDTH":
                    return VbValue.FromDouble(_window.Width * FormDesignModel.TwipsPerPixel);

                case "HEIGHT":
                    return VbValue.FromDouble(_window.Height * FormDesignModel.TwipsPerPixel);

                case "VISIBLE":
                    return VbValue.FromBoolean(_window.Visibility == Visibility.Visible);

                case "ENABLED":
                    return VbValue.FromBoolean(_window.IsEnabled);

                default:
                    // Si la propiedad coincide con el nombre de un control hijo (e.g. Me.Text1)
                    if (_controls.TryGetValue(propertyName, out var childCtrl))
                    {
                        return VbValue.FromObject(childCtrl);
                    }
                    return VbValue.Empty;
            }
        });
    }

    public void SetProperty(string propertyName, VbValue value)
    {
        if (string.IsNullOrWhiteSpace(propertyName)) return;

        _window.Dispatcher.Invoke(() =>
        {
            switch (propertyName.ToUpperInvariant())
            {
                case "CAPTION":
                case "TITLE":
                    _window.Title = value.AsString();
                    _model.Caption = value.AsString();
                    break;

                case "LEFT":
                    _window.Left = value.AsDouble() / FormDesignModel.TwipsPerPixel;
                    break;

                case "TOP":
                    _window.Top = value.AsDouble() / FormDesignModel.TwipsPerPixel;
                    break;

                case "WIDTH":
                    _window.Width = Math.Max(100, value.AsDouble() / FormDesignModel.TwipsPerPixel);
                    break;

                case "HEIGHT":
                    _window.Height = Math.Max(50, value.AsDouble() / FormDesignModel.TwipsPerPixel);
                    break;

                case "VISIBLE":
                    _window.Visibility = value.AsBoolean() ? Visibility.Visible : Visibility.Hidden;
                    break;

                case "ENABLED":
                    _window.IsEnabled = value.AsBoolean();
                    break;
            }
        });
    }

    public VbValue InvokeMethod(string methodName, VbValue[] args)
    {
        return _window.Dispatcher.Invoke(() =>
        {
            switch (methodName.ToUpperInvariant())
            {
                case "SHOW":
                    _window.Show();
                    _window.Activate();
                    return VbValue.Empty;

                case "HIDE":
                    _window.Hide();
                    return VbValue.Empty;

                case "SETFOCUS":
                    _window.Activate();
                    return VbValue.Empty;

                case "REFRESH":
                    _window.UpdateLayout();
                    return VbValue.Empty;

                case "CLS":
                    return VbValue.Empty;

                default:
                    return VbValue.Empty;
            }
        });
    }
}
