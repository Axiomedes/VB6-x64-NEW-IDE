using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using ViBa7.ActiveXHost.Native;

namespace ViBa7.ActiveXHost;

public class DesignMouseEventArgs : EventArgs
{
    public int X { get; }
    public int Y { get; }
    public MouseButtons Button { get; }
    public int Clicks { get; }

    public DesignMouseEventArgs(int x, int y, MouseButtons button, int clicks)
    {
        X = x;
        Y = y;
        Button = button;
        Clicks = clicks;
    }
}

/// <summary>
/// Implementación COM de IPropertyBag e IErrorLog para inicializar controles OCX de VB6 y terceros
/// mediante IPersistPropertyBag::Load, garantizando que UserControl_ReadProperties reciba los valores
/// del archivo .frm o utilice los valores por defecto del autor del control (evitando divisiones por cero).
/// </summary>
public sealed class VbPropertyBag : IPropertyBag, IErrorLog
{
    private const int S_OK = 0;
    private const int E_INVALIDARG = unchecked((int)0x80070057);

    private readonly Dictionary<string, object> _values = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, object> SavedProperties => _values;

    public VbPropertyBag(IDictionary<string, object>? initialValues = null)
    {
        if (initialValues != null)
        {
            foreach (var kvp in initialValues)
            {
                _values[kvp.Key] = kvp.Value;
            }
        }
    }

    public int Read(string pszPropName, ref object pVar, IErrorLog? pErrorLog)
    {
        if (string.IsNullOrWhiteSpace(pszPropName))
            return E_INVALIDARG;

        if (_values.TryGetValue(pszPropName, out var storedVal) && storedVal != null)
        {
            try
            {
                Type? targetType = pVar?.GetType();
                if (targetType != null && targetType != typeof(object))
                {
                    if (targetType == typeof(bool))
                    {
                        string str = storedVal.ToString()?.Trim() ?? string.Empty;
                        pVar = str == "-1" || str.Equals("True", StringComparison.OrdinalIgnoreCase);
                        return S_OK;
                    }
                    if (targetType == typeof(int) || targetType == typeof(short) || targetType == typeof(long))
                    {
                        string str = storedVal.ToString()?.Trim() ?? string.Empty;
                        if (str.StartsWith("&H", StringComparison.OrdinalIgnoreCase))
                        {
                            long hexVal = Convert.ToInt64(str[2..].TrimEnd('&'), 16);
                            pVar = Convert.ChangeType(hexVal, targetType, CultureInfo.InvariantCulture);
                            return S_OK;
                        }
                    }

                    pVar = Convert.ChangeType(storedVal, targetType, CultureInfo.InvariantCulture);
                }
                else
                {
                    pVar = storedVal;
                }
                return S_OK;
            }
            catch
            {
                pVar = storedVal;
                return S_OK;
            }
        }

        // Retornar E_INVALIDARG indica a msvbvm60.dll que debe usar el DefaultValue de ReadProperty(Name, Default)
        return E_INVALIDARG;
    }

    public int Write(string pszPropName, ref object pVar)
    {
        if (!string.IsNullOrWhiteSpace(pszPropName) && pVar != null)
        {
            _values[pszPropName] = pVar;
        }
        return S_OK;
    }

    public int AddError(string pszPropName, IntPtr pExcepInfo) => S_OK;
}

/// <summary>
/// Sitio de diseño (ISite) que informa al contenedor y al control ActiveX que se encuentra en Modo Diseño
/// (DISPID_AMBIENT_USERMODE = False).
/// </summary>
public sealed class ActiveXDesignSite : ISite
{
    private readonly DynamicActiveXControl _control;
    private string _name;

    public ActiveXDesignSite(DynamicActiveXControl control, string name = "ActiveXControl1")
    {
        _control = control;
        _name = name;
    }

    public IComponent Component => _control;
    public IContainer? Container => null;
    public bool DesignMode => _control.IsDesignMode;

    public string? Name
    {
        get => _name;
        set => _name = value ?? "ActiveXControl1";
    }

    public object? GetService(Type serviceType) => null;
}

/// <summary>
/// Sitio OLE nativo (IOleClientSite + IOleInPlaceSite + IOleInPlaceFrame + IOleControlSite + Ambient IDispatch)
/// que hospeda controles ActiveX tanto con ventana (Windowed) como sin ventana (Windowless) sin depender
/// de System.Windows.Forms.AxHost, proveyendo punteros de marco OLE reales para controles de MFC, Delphi y Sheridan.
/// </summary>
[ComVisible(true)]
[ClassInterface(ClassInterfaceType.AutoDispatch)]
public sealed class OleDesignClientSite : IOleClientSite, IOleInPlaceSite, IOleInPlaceFrame, IOleControlSite
{
    private const int S_OK = 0;
    private const int S_FALSE = 1;
    private const int E_NOTIMPL = unchecked((int)0x80004001);
    private const int E_NOINTERFACE = unchecked((int)0x80004002);

    private readonly DynamicActiveXControl _owner;

    public OleDesignClientSite(DynamicActiveXControl owner)
    {
        _owner = owner;
    }

    // Propiedades Ambientales estándar OLE (DISPID_AMBIENT_*) consultadas por msvbvm60.dll y controles vía IDispatch
    [DispId(-701)]
    public int BackColor => _owner.AmbientBackColorOle;

    [DispId(-702)]
    public string DisplayName => !string.IsNullOrWhiteSpace(_owner.Name) ? _owner.Name : "ActiveXControl1";

    [DispId(-704)]
    public int ForeColor => _owner.AmbientForeColorOle;

    [DispId(-705)]
    public int LocaleID => CultureInfo.CurrentUICulture.LCID;

    [DispId(-706)]
    public string ScaleUnits => "Twip";

    [DispId(-709)]
    public bool UserMode => !_owner.IsDesignMode;

    [DispId(-710)]
    public bool UIDead => false;

    [DispId(-711)]
    public bool ShowGrabHandles => false;

    [DispId(-712)]
    public bool ShowHatching => false;

    [DispId(-713)]
    public bool DisplayAsDefault => false;

    [DispId(-714)]
    public bool SupportsMnemonics => true;

    [DispId(-725)]
    public bool RightToLeft => false;

    // IOleClientSite
    public int SaveObject() => S_OK;

    public int GetMoniker(int dwAssign, int dwWhichMoniker, out IntPtr ppmk)
    {
        ppmk = IntPtr.Zero;
        return E_NOTIMPL;
    }

    public int GetContainer(out IntPtr ppContainer)
    {
        ppContainer = IntPtr.Zero;
        return E_NOINTERFACE;
    }

    public int ShowObject() => S_OK;

    public int OnShowWindow(bool fShow) => S_OK;

    public int RequestNewObjectLayout() => E_NOTIMPL;

    // IOleInPlaceSite & IOleInPlaceUIWindow & IOleInPlaceFrame
    public int GetWindow(out IntPtr phwnd)
    {
        phwnd = DynamicActiveXControl.EnsureSharedParkingHwnd();
        return phwnd != IntPtr.Zero ? S_OK : E_NOTIMPL;
    }

    public int ContextSensitiveHelp(bool fEnterMode) => S_OK;

    public int CanInPlaceActivate() => S_OK;

    public int OnInPlaceActivate() => S_OK;

    public int OnUIActivate() => S_OK;

    public int GetWindowContext(
        out IntPtr ppFrame,
        out IntPtr ppDoc,
        out COMRECT lprcPosRect,
        out COMRECT lprcClipRect,
        ref OLEINPLACEFRAMEINFO lpFrameInfo)
    {
        // Vital: Controles de MFC (COleControl::OnActivateInPlace) y Delphi desreferencian ppFrame sin validar null.
        ppFrame = Marshal.GetComInterfaceForObject(this, typeof(IOleInPlaceFrame));
        ppDoc = IntPtr.Zero;
        int w = Math.Max(16, _owner.Width);
        int h = Math.Max(16, _owner.Height);
        lprcPosRect = new COMRECT(0, 0, w, h);
        lprcClipRect = new COMRECT(0, 0, w, h);

        if (lpFrameInfo.cb >= Marshal.SizeOf<OLEINPLACEFRAMEINFO>())
        {
            lpFrameInfo.fMDIApp = 0;
            lpFrameInfo.hwndFrame = DynamicActiveXControl.EnsureSharedParkingHwnd();
            lpFrameInfo.hAccel = IntPtr.Zero;
            lpFrameInfo.cAccelEntries = 0;
        }
        return S_OK;
    }

    public int Scroll(SIZEL scrollExtant) => S_OK;

    public int OnUIDeactivate(bool fUndoable) => S_OK;

    public int OnInPlaceDeactivate() => S_OK;

    public int DiscardUndoState() => S_OK;

    public int DeactivateAndUndo() => S_OK;

    public int OnPosRectChange(ref COMRECT lprcPosRect) => S_OK;

    // IOleInPlaceUIWindow
    public int GetBorder(ref COMRECT lprectBorder) => S_OK;
    public int RequestBorderSpace(ref COMRECT pborderwidths) => S_OK;
    public int SetBorderSpace(ref COMRECT pborderwidths) => S_OK;
    public int SetActiveObject(object? pActiveObj, string? pszObjName) => S_OK;

    // IOleInPlaceFrame
    public int InsertMenus(IntPtr hmenuShared, IntPtr lpMenuWidths) => S_OK;
    public int SetMenu(IntPtr hmenuShared, IntPtr holemenu, IntPtr hwndActiveObject) => S_OK;
    public int RemoveMenus(IntPtr hmenuShared) => S_OK;
    public int SetStatusText(string pszStatusText) => S_OK;
    public int EnableModeless(bool fEnable) => S_OK;
    public int TranslateAccelerator(ref Native.MSG lpmsg, ushort wID) => S_FALSE;

    // IOleControlSite
    public int OnControlInfoChanged() => S_OK;
    public int LockInPlaceActive(bool fLock) => S_OK;
    public int GetExtendedControl(out object? ppDisp) { ppDisp = null; return E_NOTIMPL; }
    public int TransformCoords(ref POINT pPtlHimetric, ref POINT pPtfContainer, uint dwFlags) => S_OK;
    public int TranslateAccelerator(ref Native.MSG pMsg, uint grfModifiers) => S_FALSE;
    public int OnFocus(bool fGotFocus) => S_OK;
    public int ShowPropertyFrame() => S_OK;
}

/// <summary>
/// Contenedor OLE directo que aloja dinámicamente cualquier control ActiveX (OCX) con o sin ventana
/// (Windowed y Windowless) por su CLSID, sin heredar de System.Windows.Forms.AxHost (evitando el fallo
/// NotSupportedException en EnsureWindowPresent y los cuadros de excepción de NativeWindow.Callback).
/// </summary>
public class DynamicActiveXControl : IComponent, IDisposable
{
    [ThreadStatic]
    private static IntPtr _sharedParkingHwnd;

    private static readonly HashSet<string> _faultedClsids = new(StringComparer.OrdinalIgnoreCase);

    private readonly Guid _clsidGuid;
    private readonly OleDesignClientSite _oleClientSite;
    private readonly Dictionary<string, string> _pendingProperties = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _discoveredProperties = new(StringComparer.OrdinalIgnoreCase);

    private object? _ocxInstance;
    private bool _persistenceInitialized;
    private bool _disposed;
    private int _width;
    private int _height;

    public string Clsid { get; }

    /// <summary>
    /// Propiedades persistentes o expuestas descubiertas en vivo mediante IPersistPropertyBag e IDispatch.
    /// </summary>
    public IReadOnlyDictionary<string, string> DiscoveredProperties => _discoveredProperties;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Name { get; set; } = "ActiveXControl1";

    /// <summary>
    /// Cuando es true, el control está en Modo Diseño (DISPID_AMBIENT_USERMODE = False).
    /// </summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsDesignMode { get; set; } = true;

    /// <summary>
    /// Indica si ocurrió un error durante la inicialización o activación COM del control.
    /// </summary>
    public bool HasInitializationError { get; private set; }

    /// <summary>
    /// Indica si el control OCX es sin ventana (Windowless) o no creó un HWND propio.
    /// </summary>
    public bool IsWindowless { get; private set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int AmbientBackColorOle { get; set; } = unchecked((int)0x8000000F);

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int AmbientForeColorOle { get; set; } = unchecked((int)0x80000012);

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ISite? Site { get; set; }

    public event EventHandler? Disposed;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Width
    {
        get => _width;
        set => SetBounds(value, _height);
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Height
    {
        get => _height;
        set => SetBounds(_width, value);
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public System.Drawing.Size Size
    {
        get => new(_width, _height);
        set => SetBounds(value.Width, value.Height);
    }

#pragma warning disable CS0067
    public event EventHandler<DesignMouseEventArgs>? DesignMouseDown;
    public event EventHandler<DesignMouseEventArgs>? DesignMouseMove;
    public event EventHandler<DesignMouseEventArgs>? DesignMouseUp;
    public event EventHandler<DesignMouseEventArgs>? DesignMouseDoubleClick;
    public event EventHandler<DesignMouseEventArgs>? DesignContextMenu;
#pragma warning restore CS0067

    public DynamicActiveXControl(string clsid, int initialWidth = 120, int initialHeight = 36)
    {
        Ole32Interop.MaskFpuExceptions();
        Clsid = NormalizeClsid(clsid);
        _clsidGuid = Guid.TryParse(Clsid, out var g) ? g : Guid.Empty;
        _width = Math.Max(16, initialWidth);
        _height = Math.Max(16, initialHeight);
        _oleClientSite = new OleDesignClientSite(this);
    }

    internal static IntPtr EnsureSharedParkingHwnd()
    {
        if (_sharedParkingHwnd == IntPtr.Zero)
        {
            _sharedParkingHwnd = Ole32Interop.CreateWindowExW(
                Ole32Interop.WS_EX_TOOLWINDOW | Ole32Interop.WS_EX_NOACTIVATE,
                "STATIC",
                "ViBa7OleParkingHost",
                Ole32Interop.WS_POPUP | Ole32Interop.WS_CLIPCHILDREN | Ole32Interop.WS_CLIPSIBLINGS,
                -32000,
                -32000,
                1024,
                1024,
                IntPtr.Zero,
                IntPtr.Zero,
                IntPtr.Zero,
                IntPtr.Zero);
        }
        return _sharedParkingHwnd;
    }

    private void SetBounds(int width, int height)
    {
        int safeW = Math.Max(16, width);
        int safeH = Math.Max(16, height);
        bool changed = (safeW != _width || safeH != _height);
        _width = safeW;
        _height = safeH;

        if (changed && _ocxInstance != null && !HasInitializationError)
        {
            UpdateOleBoundsAndExtent(safeW, safeH);
        }
    }

    private void UpdateOleBoundsAndExtent(int widthPx, int heightPx)
    {
        using var guard = new NativeDialogGuard();
        try
        {
            if (_ocxInstance is IOleObject oleObj)
            {
                // Conversión de píxeles (96 DPI) a unidades HIMETRIC (2540 por pulgada)
                int cxHimetric = Math.Max(100, (int)Math.Round(widthPx * 2540.0 / 96.0));
                int cyHimetric = Math.Max(100, (int)Math.Round(heightPx * 2540.0 / 96.0));
                var sizel = new SIZEL(cxHimetric, cyHimetric);
                oleObj.SetExtent(Ole32Interop.DVASPECT_CONTENT, ref sizel);
            }

            if (_ocxInstance is IOleInPlaceObject inPlaceObj)
            {
                var rect = new COMRECT(0, 0, widthPx, heightPx);
                inPlaceObj.SetObjectRects(ref rect, ref rect);
            }
        }
        catch
        {
            // Ignorar errores de redimensionamiento en controles sin ventana o pasivos
        }
    }

    private static string NormalizeClsid(string clsid)
    {
        string clean = clsid.Trim();
        if (Guid.TryParse(clean, out Guid g))
        {
            return g.ToString("B").ToUpperInvariant();
        }
        return clean;
    }

    /// <summary>
    /// Instancia e inicializa el objeto COM ActiveX (tanto con ventana como sin ventana) en modo diseño.
    /// </summary>
    public bool CreateControl()
    {
        if (_disposed || HasInitializationError) return false;
        if (_ocxInstance != null) return true;
        if (_clsidGuid == Guid.Empty)
        {
            HasInitializationError = true;
            return false;
        }

        // Si el control ya falló catastróficamente en este CLSID previamente, omitir para proteger la estabilidad del IDE
        if (_faultedClsids.Contains(Clsid))
        {
            HasInitializationError = true;
            return false;
        }

        using var guard = new NativeDialogGuard();
        try
        {
            Guid clsid = _clsidGuid;
            Guid iidUnknown = Ole32Interop.IID_IUnknown;

            int hr = Ole32Interop.CoCreateInstance(
                ref clsid,
                IntPtr.Zero,
                Ole32Interop.CLSCTX_INPROC_SERVER | Ole32Interop.CLSCTX_LOCAL_SERVER,
                ref iidUnknown,
                out _ocxInstance);

            if (hr < 0 || _ocxInstance == null)
            {
                HasInitializationError = true;
                return false;
            }

            // 1. Vincular el sitio de diseño (expone Ambient.UserMode = False para diseño)
            if (_ocxInstance is IOleObject oleObj)
            {
                try { oleObj.SetClientSite(_oleClientSite); } catch { }
                try { oleObj.SetHostNames("ViBa7 IDE", Name); } catch { }
            }

            // 2. Inicializar estado de persistencia (IPersistPropertyBag / IPersistStreamInit) ANTES de disparar Resize/SetExtent
            InitializePersistenceState(_ocxInstance);

            // 3. Aplicar propiedades del .frm vía IDispatch
            if (!HasInitializationError)
            {
                ApplyPendingPropertiesToOcx(_ocxInstance);
            }

            // 4. Establecer dimensiones HIMETRIC tras la correcta inicialización de propiedades
            UpdateOleBoundsAndExtent(_width, _height);

            // 5. En modo diseño, si el control tiene IViewObject (OleDraw), opera de forma pasiva sin activar hooks invasivos
            // Solo si no es modo diseño o se requiere activación In-Place explícita, se llama DoVerb
            if (_ocxInstance is IOleObject oleActivate && !guard.DialogSuppressed)
            {
                try
                {
                    IntPtr hostHwnd = EnsureSharedParkingHwnd();
                    var posRect = new COMRECT(0, 0, _width, _height);
                    oleActivate.DoVerb(
                        Ole32Interop.OLEIVERB_INPLACEACTIVATE,
                        IntPtr.Zero,
                        _oleClientSite,
                        0,
                        hostHwnd,
                        ref posRect);
                }
                catch
                {
                    // Los controles ActiveX sin ventana (Windowless) o solo-diseño funcionan vía IViewObject/OleDraw sin HWND
                    IsWindowless = true;
                }
            }

            // Determinar si el control creó HWND o es Windowless
            if (_ocxInstance is IOleInPlaceObject inPlace)
            {
                if (inPlace.GetWindow(out IntPtr ctrlHwnd) != 0 || ctrlHwnd == IntPtr.Zero)
                {
                    IsWindowless = true;
                }
            }
            else
            {
                IsWindowless = true;
            }

            if (guard.DialogSuppressed)
            {
                HasInitializationError = true;
                return false;
            }

            return true;
        }
        catch
        {
            _faultedClsids.Add(Clsid);
            HasInitializationError = true;
            return false;
        }
    }

    private void InitializePersistenceState(object ocx)
    {
        if (_persistenceInitialized) return;
        _persistenceInitialized = true;

        var typedProps = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        int extentX = Math.Max(423, (int)Math.Round(_width * 2540.0 / 96.0));
        int extentY = Math.Max(423, (int)Math.Round(_height * 2540.0 / 96.0));
        typedProps["_ExtentX"] = extentX;
        typedProps["_ExtentY"] = extentY;

        foreach (var kvp in _pendingProperties)
        {
            var converted = ConvertPropertyStringToOcxType(kvp.Key, kvp.Value);
            if (converted != null)
            {
                typedProps[kvp.Key] = converted;
            }
        }

        if (ocx is IPersistPropertyBag persistBag)
        {
            try
            {
                persistBag.InitNew();
            }
            catch
            {
                // Ignorar si requiere Load directo
            }

            if (typedProps.Count > 0)
            {
                try
                {
                    var bag = new VbPropertyBag(typedProps);
                    persistBag.Load(bag, bag);
                }
                catch
                {
                    // Ignorar si alguna propiedad individual no es admitida
                }
            }

            // Descubrir todas las propiedades persistidas reales y sus valores por defecto
            try
            {
                var saveBag = new VbPropertyBag();
                persistBag.Save(saveBag, fClearDirty: false, fSaveAllProperties: true);
                foreach (var kvp in saveBag.SavedProperties)
                {
                    if (!_discoveredProperties.ContainsKey(kvp.Key) && kvp.Value != null)
                    {
                        _discoveredProperties[kvp.Key] = kvp.Value.ToString() ?? string.Empty;
                    }
                }
            }
            catch
            {
                // Ignorar si Save no está disponible en este control
            }
        }
        else if (ocx is IPersistStreamInit persistStream)
        {
            try
            {
                persistStream.InitNew();
            }
            catch
            {
                // Ignorar
            }
        }
    }

    /// <summary>
    /// Obtiene la referencia al objeto COM subyacente (IDispatch / IUnknown) del control ActiveX.
    /// </summary>
    public object? GetActiveXObject()
    {
        if (HasInitializationError) return null;
        return _ocxInstance;
    }

    #region Captura Visual Libre de Airspace y Verificación de Pintado

    /// <summary>
    /// Renderiza el control ActiveX (con o sin ventana) a un BitmapSource de WPF utilizando OleDraw (IViewObject::Draw)
    /// y WM_PRINT, verificando que el resultado no sea un recuadro transparente o sin pintar.
    /// </summary>
    public BitmapSource? TryCaptureVisualSnapshot(int widthPx, int heightPx)
    {
        if (_disposed || HasInitializationError || _ocxInstance == null) return null;

        int w = Math.Max(16, Math.Min(4096, widthPx));
        int h = Math.Max(16, Math.Min(4096, heightPx));

        using var guard = new NativeDialogGuard();
        try
        {
            SetBounds(w, h);

            using var bmp = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
            Color baseBg = ResolveBackgroundColor();

            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(baseBg);
                var rect = new COMRECT(0, 0, w, h);
                IntPtr hdc = g.GetHdc();
                try
                {
                    // 1. OleDraw (IViewObject::Draw): método universal de OLE para controles con ventana y sin ventana (Windowless)
                    int hr = Ole32Interop.OleDraw(_ocxInstance, Ole32Interop.DVASPECT_CONTENT, hdc, ref rect);

                    // 2. Si el control tiene HWND propio y OleDraw falló, intentar captura de ventana Win32
                    if (hr < 0 && _ocxInstance is IOleInPlaceObject inPlace &&
                        inPlace.GetWindow(out IntPtr ctrlHwnd) == 0 && ctrlHwnd != IntPtr.Zero)
                    {
                        Ole32Interop.PrintWindow(ctrlHwnd, hdc, 0);
                        Ole32Interop.SendMessageW(
                            ctrlHwnd,
                            Ole32Interop.WM_PRINT,
                            hdc,
                            (IntPtr)(Ole32Interop.PRF_CLIENT | Ole32Interop.PRF_NONCLIENT | Ole32Interop.PRF_CHILDREN | Ole32Interop.PRF_ERASEBKGND));
                    }
                }
                finally
                {
                    g.ReleaseHdc(hdc);
                }

                // Si OleDraw devolvió S_OK pero el control pintó en su propio HWND hijo en lugar de IViewObject
                if (!IsBitmapVisuallyValid(bmp) && _ocxInstance is IOleInPlaceObject inPlaceFallback &&
                    inPlaceFallback.GetWindow(out IntPtr hwndChild) == 0 && hwndChild != IntPtr.Zero)
                {
                    IntPtr hdc2 = g.GetHdc();
                    try
                    {
                        Ole32Interop.SendMessageW(
                            hwndChild,
                            Ole32Interop.WM_PRINT,
                            hdc2,
                            (IntPtr)(Ole32Interop.PRF_CLIENT | Ole32Interop.PRF_NONCLIENT | Ole32Interop.PRF_CHILDREN | Ole32Interop.PRF_ERASEBKGND));
                    }
                    finally
                    {
                        g.ReleaseHdc(hdc2);
                    }
                }
            }

            if (guard.DialogSuppressed)
            {
                HasInitializationError = true;
                return null;
            }

            if (!IsBitmapVisuallyValid(bmp))
            {
                return null;
            }

            IntPtr hBitmap = bmp.GetHbitmap();
            try
            {
                var source = Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
                return source;
            }
            finally
            {
                Ole32Interop.DeleteObject(hBitmap);
            }
        }
        catch
        {
            HasInitializationError = true;
            return null;
        }
    }

    private Color ResolveBackgroundColor()
    {
        if (_pendingProperties.TryGetValue("BackColor", out var bgStr) && !string.IsNullOrWhiteSpace(bgStr))
        {
            if (bgStr.StartsWith("&H", StringComparison.OrdinalIgnoreCase))
            {
                string hex = bgStr[2..].TrimEnd('&');
                if (uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint oleCol))
                {
                    AmbientBackColorOle = unchecked((int)oleCol);
                    if ((oleCol & 0x80000000) == 0)
                    {
                        int r = (int)(oleCol & 0xFF);
                        int gr = (int)((oleCol >> 8) & 0xFF);
                        int b = (int)((oleCol >> 16) & 0xFF);
                        return Color.FromArgb(255, r, gr, b);
                    }
                }
            }
        }
        return Color.FromArgb(255, 240, 240, 240);
    }

    /// <summary>
    /// Verifica si el bitmap capturado contiene un dibujo real (bordes, texto, iconos o gradientes)
    /// y no es simplemente un rectángulo vacío/transparente de un solo color plano.
    /// </summary>
    public static bool IsBitmapVisuallyValid(Bitmap bmp)
    {
        if (bmp.Width < 4 || bmp.Height < 4) return false;

        Color first = bmp.GetPixel(bmp.Width / 2, bmp.Height / 2);
        int distinctPixels = 0;

        int stepX = Math.Max(1, bmp.Width / 16);
        int stepY = Math.Max(1, bmp.Height / 16);

        for (int y = 0; y < bmp.Height; y += stepY)
        {
            for (int x = 0; x < bmp.Width; x += stepX)
            {
                Color c = bmp.GetPixel(x, y);
                if (c.A > 10 && (Math.Abs(c.R - first.R) > 3 || Math.Abs(c.G - first.G) > 3 || Math.Abs(c.B - first.B) > 3))
                {
                    distinctPixels++;
                    if (distinctPixels >= 2)
                    {
                        return true;
                    }
                }
            }
        }

        // Revisar los bordes (1px interior y exterior) por si el control solo dibuja un marco 3D alrededor
        for (int x = 0; x < bmp.Width; x += Math.Max(1, bmp.Width / 20))
        {
            Color top = bmp.GetPixel(x, 0);
            Color bottom = bmp.GetPixel(x, bmp.Height - 1);
            if (Math.Abs(top.R - first.R) > 3 || Math.Abs(bottom.R - first.R) > 3 ||
                Math.Abs(top.G - first.G) > 3 || Math.Abs(bottom.G - first.G) > 3)
            {
                return true;
            }
        }

        return false;
    }

    #endregion

    #region Sincronización de Propiedades vía IDispatch

    public object? GetPropertyValue(string propertyName)
    {
        var ocx = GetActiveXObject();
        if (ocx == null || string.IsNullOrWhiteSpace(propertyName)) return null;

        try
        {
            return ocx.GetType().InvokeMember(
                propertyName,
                BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase,
                null,
                ocx,
                null,
                CultureInfo.InvariantCulture);
        }
        catch
        {
            return null;
        }
    }

    public bool SetPropertyValue(string propertyName, object? value)
    {
        if (string.IsNullOrWhiteSpace(propertyName)) return false;

        if (value != null)
        {
            _pendingProperties[propertyName] = value.ToString() ?? string.Empty;
        }

        var ocx = GetActiveXObject();
        if (ocx == null) return false;

        using var guard = new NativeDialogGuard();
        try
        {
            ocx.GetType().InvokeMember(
                propertyName,
                BindingFlags.SetProperty | BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase,
                null,
                ocx,
                new object?[] { value },
                CultureInfo.InvariantCulture);
            return !guard.DialogSuppressed;
        }
        catch
        {
            return false;
        }
    }

    public void ApplyProperties(IDictionary<string, string> properties)
    {
        if (properties == null) return;

        foreach (var kvp in properties)
        {
            _pendingProperties[kvp.Key] = kvp.Value;
        }

        if (_ocxInstance != null && !HasInitializationError)
        {
            ApplyPendingPropertiesToOcx(_ocxInstance);
        }
    }

    private void ApplyPendingPropertiesToOcx(object ocx)
    {
        if (_pendingProperties.Count == 0) return;

        using var guard = new NativeDialogGuard();
        foreach (var kvp in _pendingProperties)
        {
            string propName = kvp.Key;
            string rawVal = kvp.Value;

            if (propName.StartsWith("_", StringComparison.Ordinal) ||
                propName.Equals("Name", StringComparison.OrdinalIgnoreCase) ||
                propName.Equals("Index", StringComparison.OrdinalIgnoreCase) ||
                propName.Equals("Left", StringComparison.OrdinalIgnoreCase) ||
                propName.Equals("Top", StringComparison.OrdinalIgnoreCase) ||
                propName.Equals("Width", StringComparison.OrdinalIgnoreCase) ||
                propName.Equals("Height", StringComparison.OrdinalIgnoreCase) ||
                propName.Equals("TabIndex", StringComparison.OrdinalIgnoreCase) ||
                propName.Equals("TabStop", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                object? converted = ConvertPropertyStringToOcxType(propName, rawVal);
                if (converted != null)
                {
                    ocx.GetType().InvokeMember(
                        propName,
                        BindingFlags.SetProperty | BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase,
                        null,
                        ocx,
                        new object?[] { converted },
                        CultureInfo.InvariantCulture);
                }

                if (guard.DialogSuppressed)
                {
                    HasInitializationError = true;
                    break;
                }
            }
            catch
            {
                // Ignorar propiedades de solo lectura o con tipos especiales
            }
        }
    }

    public static object? ConvertPropertyStringToOcxType(string propertyName, string strValue)
    {
        if (string.IsNullOrWhiteSpace(strValue)) return strValue;

        if (strValue.Contains(".frx\":", StringComparison.OrdinalIgnoreCase) ||
            strValue.Contains(".ctx\":", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (strValue.Equals("True", StringComparison.OrdinalIgnoreCase))
            return true;
        if (strValue.Equals("False", StringComparison.OrdinalIgnoreCase))
            return false;

        // Solo interpretar -1 / 0 como booleano si la propiedad es conocida por ser booleana.
        // De lo contrario, propiedades como BorderStyle = 0, Style = 0, Appearance = 0, Alignment = 0 se corrompen a false.
        if (IsKnownBooleanProperty(propertyName))
        {
            if (strValue == "-1") return true;
            if (strValue == "0") return false;
        }

        if (strValue.StartsWith("&H", StringComparison.OrdinalIgnoreCase))
        {
            string hexClean = strValue[2..].TrimEnd('&');
            if (long.TryParse(hexClean, NumberStyles.HexNumber, null, out long hexVal))
            {
                return (uint)hexVal;
            }
        }

        if (int.TryParse(strValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intVal))
            return intVal;

        if (double.TryParse(strValue, NumberStyles.Float, CultureInfo.InvariantCulture, out double dblVal))
            return dblVal;

        return strValue.Trim('\"');
    }

    private static bool IsKnownBooleanProperty(string propertyName)
    {
        return propertyName.Equals("Enabled", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Equals("Visible", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Equals("Locked", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Equals("TabStop", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Equals("AutoSize", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Equals("WordWrap", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Equals("MultiLine", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Equals("Default", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Equals("Cancel", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Equals("CausesValidation", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Equals("AutoRedraw", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Equals("ClipControls", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Equals("RightToLeft", StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Hojas de Propiedades OLE Nativas (OleCreatePropertyFrame)

    public bool HasPropertyPages()
    {
        var ocx = GetActiveXObject();
        if (ocx is not ISpecifyPropertyPages specifyPages) return false;

        try
        {
            int hr = specifyPages.GetPages(out var pages);
            bool has = hr == 0 && pages.cElems > 0;
            if (pages.pElems != IntPtr.Zero)
            {
                Ole32Interop.CoTaskMemFree(pages.pElems);
            }
            return has;
        }
        catch
        {
            return false;
        }
    }

    public bool ShowPropertyPages(IntPtr ownerHwnd, string? caption = null)
    {
        var ocx = GetActiveXObject();
        if (ocx is not ISpecifyPropertyPages specifyPages) return false;

        try
        {
            int hr = specifyPages.GetPages(out var pages);
            if (hr != 0 || pages.cElems == 0 || pages.pElems == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                string dlgCaption = caption ?? $"Propiedades de {Name}";
                object[] objs = { ocx };

                hr = Ole32Interop.OleCreatePropertyFrame(
                    ownerHwnd,
                    0,
                    0,
                    dlgCaption,
                    1,
                    objs,
                    pages.cElems,
                    pages.pElems,
                    0,
                    0,
                    IntPtr.Zero);

                return hr == 0;
            }
            finally
            {
                Ole32Interop.CoTaskMemFree(pages.pElems);
            }
        }
        catch
        {
            return false;
        }
    }

    #endregion

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            if (_ocxInstance != null)
            {
                if (_ocxInstance is IOleInPlaceObject inPlace)
                {
                    try { inPlace.InPlaceDeactivate(); } catch { }
                }
                if (_ocxInstance is IOleObject oleObj)
                {
                    try { oleObj.Close(Ole32Interop.OLECLOSE_NOSAVE); } catch { }
                    try { oleObj.SetClientSite(null); } catch { }
                }
                if (Marshal.IsComObject(_ocxInstance))
                {
                    try { Marshal.ReleaseComObject(_ocxInstance); } catch { }
                }
                _ocxInstance = null;
            }
        }
        catch
        {
            // Ignorar errores al liberar COM
        }

        Disposed?.Invoke(this, EventArgs.Empty);
    }
}
