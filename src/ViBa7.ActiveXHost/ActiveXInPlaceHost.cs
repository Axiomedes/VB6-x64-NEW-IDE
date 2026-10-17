using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using ViBa7.ActiveXHost.Native;
using ViBa7.ProjectModel.Designers;

namespace ViBa7.ActiveXHost;

/// <summary>
/// Contenedor visual de WPF para controles ActiveX (OCX) en la superficie de diseño de ViBa7.
/// Mantiene la instancia COM real en proceso (para reflexión IDispatch, IPersistPropertyBag y hojas
/// de propiedades OLE nativas) y renderiza su apariencia verificada libre de parpadeos (flicker)
/// y sin agujeros transparentes de Airspace, con respaldo visual automático de alta fidelidad.
/// </summary>
public class ActiveXInPlaceHost : Border, IDisposable
{
    private readonly DynamicActiveXControl _axControl;
    private readonly Image _snapshotImage;
    private FrameworkElement? _fallbackVisual;
    private bool _nativeActivated;
    private bool _disposed;

    public string Clsid => _axControl.Clsid;
    public string ProgId { get; }
    public DynamicActiveXControl AxControl => _axControl;
    public FormDesignerControl? BoundControl { get; set; }

    /// <summary>
    /// Indica si el control OCX nativo produjo una captura gráfica válida en modo diseño.
    /// Si es false, se muestra el FallbackVisual de alta fidelidad de WPF.
    /// </summary>
    public bool HasValidNativeVisual { get; private set; }

    /// <summary>
    /// Elemento visual de respaldo de alta fidelidad (WPF) utilizado cuando el OCX de terceros
    /// no puede dibujarse nativamente en modo diseño (por falta de licencia de diseño, estado .frx propietario, etc.).
    /// </summary>
    public FrameworkElement? FallbackVisual
    {
        get => _fallbackVisual;
        set
        {
            _fallbackVisual = value;
            if (!HasValidNativeVisual && _fallbackVisual != null)
            {
                Child = _fallbackVisual;
            }
        }
    }

    public bool IsDesignMode
    {
        get => _axControl.IsDesignMode;
        set => _axControl.IsDesignMode = value;
    }

    public event Action<ActiveXInPlaceHost, Point>? HostSelected;
    public event Action<ActiveXInPlaceHost, Point>? HostDoubleClicked;
    public event Action<ActiveXInPlaceHost, Point>? HostContextMenuRequested;
    public event Action<ActiveXInPlaceHost, Point>? HostDragStarted;
    public event Action<ActiveXInPlaceHost, Point>? HostDragging;
    public event Action<ActiveXInPlaceHost>? HostDragEnded;

    public ActiveXDesignSite DesignSite { get; }

    public ActiveXInPlaceHost(string clsid, string progId = "", int initialWidthPx = 120, int initialHeightPx = 36)
    {
        ProgId = progId;
        int safeW = Math.Max(24, initialWidthPx);
        int safeH = Math.Max(20, initialHeightPx);

        _axControl = new DynamicActiveXControl(clsid, safeW, safeH)
        {
            IsDesignMode = true,
            Name = !string.IsNullOrWhiteSpace(progId) ? progId : "ActiveXControl1"
        };
        DesignSite = new ActiveXDesignSite(_axControl, _axControl.Name);
        _axControl.Site = DesignSite;

        _snapshotImage = new Image
        {
            Stretch = Stretch.Fill,
            SnapsToDevicePixels = true,
            UseLayoutRounding = true
        };
        RenderOptions.SetBitmapScalingMode(_snapshotImage, BitmapScalingMode.NearestNeighbor);

        Background = Brushes.Transparent;
        SnapsToDevicePixels = true;
        UseLayoutRounding = true;
        ClipToBounds = true;

        HookAxEvents();
    }

    private void HookAxEvents()
    {
        _axControl.DesignMouseDown += (s, e) =>
        {
            var pt = new Point(e.X, e.Y);
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                HostSelected?.Invoke(this, pt);
                HostDragStarted?.Invoke(this, pt);
            }
        };

        _axControl.DesignMouseMove += (s, e) =>
        {
            var pt = new Point(e.X, e.Y);
            HostDragging?.Invoke(this, pt);
        };

        _axControl.DesignMouseUp += (s, e) =>
        {
            HostDragEnded?.Invoke(this);
        };

        _axControl.DesignMouseDoubleClick += (s, e) =>
        {
            var pt = new Point(e.X, e.Y);
            HostDoubleClicked?.Invoke(this, pt);
        };

        _axControl.DesignContextMenu += (s, e) =>
        {
            var pt = new Point(e.X, e.Y);
            HostContextMenuRequested?.Invoke(this, pt);
        };
    }

    /// <summary>
    /// Inicializa de forma segura la instancia COM OLE (con o sin ventana)
    /// y captura su representación visual, conmutando automáticamente al FallbackVisual
    /// si el control produce un cuadro vacío o lanza un error interno (ej. División por cero).
    /// </summary>
    public bool TryInitializeAndRender()
    {
        if (_disposed) return false;

        int w = BoundControl != null ? Math.Max(24, (int)Math.Round(BoundControl.WidthPixels)) : Math.Max(24, (int)Width);
        int h = BoundControl != null ? Math.Max(20, (int)Math.Round(BoundControl.HeightPixels)) : Math.Max(20, (int)Height);

        if (BoundControl != null && !string.IsNullOrWhiteSpace(BoundControl.Name))
        {
            _axControl.Name = BoundControl.Name;
            DesignSite.Name = BoundControl.Name;
        }

        SyncPropertiesDictionaryToAx();

        if (!_nativeActivated && !_axControl.HasInitializationError)
        {
            _axControl.Size = new System.Drawing.Size(w, h);
            _nativeActivated = _axControl.CreateControl() && !_axControl.HasInitializationError;

            // Sincronizar propiedades descubiertas en vivo desde el OCX hacia el modelo de diseño para el PropertyGrid
            if (_nativeActivated && BoundControl != null && _axControl.DiscoveredProperties.Count > 0)
            {
                foreach (var kvp in _axControl.DiscoveredProperties)
                {
                    if (!BoundControl.CustomProperties.Exists(p => p.Name.Equals(kvp.Key, StringComparison.OrdinalIgnoreCase)))
                    {
                        BoundControl.CustomProperties.Add(new FormPropertyItem
                        {
                            Name = kvp.Key,
                            Value = kvp.Value
                        });
                    }
                }
            }
        }

        return RefreshVisualSnapshot(w, h);
    }

    /// <summary>
    /// Refresca la imagen visual del control ActiveX para las dimensiones dadas,
    /// o activa el FallbackVisual si la captura nativa no es válida.
    /// </summary>
    public bool RefreshVisualSnapshot(int widthPx = 0, int heightPx = 0)
    {
        if (widthPx <= 0)
        {
            widthPx = BoundControl != null ? Math.Max(24, (int)Math.Round(BoundControl.WidthPixels)) : Math.Max(24, (int)ActualWidth);
        }
        if (heightPx <= 0)
        {
            heightPx = BoundControl != null ? Math.Max(20, (int)Math.Round(BoundControl.HeightPixels)) : Math.Max(20, (int)ActualHeight);
        }

        if (_nativeActivated && !_axControl.HasInitializationError)
        {
            var bmpSource = _axControl.TryCaptureVisualSnapshot(widthPx, heightPx);
            if (bmpSource != null)
            {
                _snapshotImage.Source = bmpSource;
                Child = _snapshotImage;
                HasValidNativeVisual = true;
                return true;
            }
        }

        HasValidNativeVisual = false;
        if (_fallbackVisual != null)
        {
            _fallbackVisual.Width = widthPx;
            _fallbackVisual.Height = heightPx;
            Child = _fallbackVisual;
        }
        return false;
    }

    private void SyncPropertiesDictionaryToAx()
    {
        if (BoundControl == null) return;

        if (!BoundControl.ExtentX.HasValue || BoundControl.ExtentX.Value <= 0 ||
            !BoundControl.ExtentY.HasValue || BoundControl.ExtentY.Value <= 0)
        {
            BoundControl.UpdateExtentsFromDimensions();
        }

        var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in BoundControl.CustomProperties)
        {
            if (!p.IsBeginProperty && !string.IsNullOrWhiteSpace(p.Name))
            {
                props[p.Name] = p.Value;
            }
        }
        if (!string.IsNullOrEmpty(BoundControl.Caption)) props["Caption"] = BoundControl.Caption;
        if (!string.IsNullOrEmpty(BoundControl.Text)) props["Text"] = BoundControl.Text;
        if (BoundControl.BackColorHex != null) props["BackColor"] = BoundControl.BackColorHex;
        if (BoundControl.ForeColorHex != null) props["ForeColor"] = BoundControl.ForeColorHex;
        if (BoundControl.BorderStyle.HasValue) props["BorderStyle"] = BoundControl.BorderStyle.Value.ToString();

        _axControl.ApplyProperties(props);
    }

    /// <summary>
    /// Muestra la hoja de propiedades nativa OLE del control ActiveX alojado.
    /// </summary>
    public bool ShowPropertyPages(Window? ownerWindow = null)
    {
        if (!_nativeActivated || _axControl.HasInitializationError) return false;

        IntPtr hwndOwner = IntPtr.Zero;
        if (ownerWindow != null)
        {
            hwndOwner = new WindowInteropHelper(ownerWindow).Handle;
        }

        string caption = BoundControl != null
            ? $"Propiedades de {BoundControl.Name} ({BoundControl.TypeName})"
            : $"Propiedades de ActiveX {ProgId}";

        bool shown = _axControl.ShowPropertyPages(hwndOwner, caption);
        if (shown)
        {
            SyncToDesignerModel();
            RefreshVisualSnapshot();
        }
        return shown;
    }

    /// <summary>
    /// Sincroniza las propiedades modificadas en la hoja de propiedades nativa OLE hacia el modelo BoundControl.
    /// </summary>
    public void SyncToDesignerModel()
    {
        if (BoundControl == null || !_nativeActivated || _axControl.HasInitializationError) return;

        foreach (var p in BoundControl.CustomProperties)
        {
            if (!p.IsBeginProperty)
            {
                var liveVal = _axControl.GetPropertyValue(p.Name);
                if (liveVal != null)
                {
                    p.Value = liveVal.ToString() ?? string.Empty;
                }
            }
        }
        foreach (var kv in _axControl.DiscoveredProperties)
        {
            var liveVal = _axControl.GetPropertyValue(kv.Key);
            if (liveVal != null)
            {
                var existing = BoundControl.CustomProperties.Find(p => p.Name.Equals(kv.Key, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    existing.Value = liveVal.ToString() ?? string.Empty;
                }
                else
                {
                    BoundControl.CustomProperties.Add(new FormPropertyItem
                    {
                        Name = kv.Key,
                        Value = liveVal.ToString() ?? string.Empty
                    });
                }
            }
        }
    }

    /// <summary>
    /// Sincroniza las propiedades del modelo de diseño FormDesignerControl hacia el control ActiveX nativo.
    /// </summary>
    public void SyncFromDesignerModel()
    {
        if (BoundControl == null) return;
        SyncPropertiesDictionaryToAx();

        if (_nativeActivated && !_axControl.HasInitializationError)
        {
            RefreshVisualSnapshot();
        }
    }

    /// <summary>
    /// Sincroniza un valor de propiedad individual modificado en el PropertyGrid hacia el control nativo.
    /// </summary>
    public void SetLiveProperty(string propertyName, object? value)
    {
        _axControl.SetPropertyValue(propertyName, value);
        if (_nativeActivated && !_axControl.HasInitializationError)
        {
            RefreshVisualSnapshot();
        }
    }

    /// <summary>
    /// Lee el valor de una propiedad directamente del control COM IDispatch.
    /// </summary>
    public object? GetLiveProperty(string propertyName)
    {
        return _axControl.GetPropertyValue(propertyName);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            _axControl.Dispose();
        }
        catch
        {
            // Ignorar errores al liberar recursos COM
        }
    }
}
