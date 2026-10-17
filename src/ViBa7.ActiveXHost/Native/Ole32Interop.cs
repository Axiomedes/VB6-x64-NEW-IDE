using System;
using System.Runtime.InteropServices;
using System.Text;

namespace ViBa7.ActiveXHost.Native;

[StructLayout(LayoutKind.Sequential)]
public struct CAUUID
{
    public int cElems;
    public IntPtr pElems;
}

[StructLayout(LayoutKind.Sequential)]
public struct COMRECT
{
    public int left;
    public int top;
    public int right;
    public int bottom;

    public COMRECT(int left, int top, int right, int bottom)
    {
        this.left = left;
        this.top = top;
        this.right = right;
        this.bottom = bottom;
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct SIZEL
{
    public int cx;
    public int cy;

    public SIZEL(int cx, int cy)
    {
        this.cx = cx;
        this.cy = cy;
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct OLEINPLACEFRAMEINFO
{
    public int cb;
    public int fMDIApp;
    public IntPtr hwndFrame;
    public IntPtr hAccel;
    public int cAccelEntries;
}

[StructLayout(LayoutKind.Sequential)]
public struct POINT
{
    public int x;
    public int y;

    public POINT(int x, int y)
    {
        this.x = x;
        this.y = y;
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct MSG
{
    public IntPtr hwnd;
    public uint message;
    public IntPtr wParam;
    public IntPtr lParam;
    public uint time;
    public POINT pt;
}

[ComImport]
[Guid("00000115-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IOleInPlaceUIWindow
{
    [PreserveSig]
    int GetWindow(out IntPtr phwnd);

    [PreserveSig]
    int ContextSensitiveHelp([MarshalAs(UnmanagedType.Bool)] bool fEnterMode);

    [PreserveSig]
    int GetBorder(ref COMRECT lprectBorder);

    [PreserveSig]
    int RequestBorderSpace(ref COMRECT pborderwidths);

    [PreserveSig]
    int SetBorderSpace(ref COMRECT pborderwidths);

    [PreserveSig]
    int SetActiveObject(
        [MarshalAs(UnmanagedType.IUnknown)] object? pActiveObj,
        [MarshalAs(UnmanagedType.LPWStr)] string? pszObjName);
}

[ComImport]
[Guid("00000116-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IOleInPlaceFrame : IOleInPlaceUIWindow
{
    [PreserveSig]
    new int GetWindow(out IntPtr phwnd);

    [PreserveSig]
    new int ContextSensitiveHelp([MarshalAs(UnmanagedType.Bool)] bool fEnterMode);

    [PreserveSig]
    new int GetBorder(ref COMRECT lprectBorder);

    [PreserveSig]
    new int RequestBorderSpace(ref COMRECT pborderwidths);

    [PreserveSig]
    new int SetBorderSpace(ref COMRECT pborderwidths);

    [PreserveSig]
    new int SetActiveObject(
        [MarshalAs(UnmanagedType.IUnknown)] object? pActiveObj,
        [MarshalAs(UnmanagedType.LPWStr)] string? pszObjName);

    [PreserveSig]
    int InsertMenus(IntPtr hmenuShared, IntPtr lpMenuWidths);

    [PreserveSig]
    int SetMenu(IntPtr hmenuShared, IntPtr holemenu, IntPtr hwndActiveObject);

    [PreserveSig]
    int RemoveMenus(IntPtr hmenuShared);

    [PreserveSig]
    int SetStatusText([MarshalAs(UnmanagedType.LPWStr)] string pszStatusText);

    [PreserveSig]
    int EnableModeless([MarshalAs(UnmanagedType.Bool)] bool fEnable);

    [PreserveSig]
    int TranslateAccelerator(ref MSG lpmsg, ushort wID);
}

[ComImport]
[Guid("B196B289-BAB4-101A-B69C-00AA00341D07")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IOleControlSite
{
    [PreserveSig]
    int OnControlInfoChanged();

    [PreserveSig]
    int LockInPlaceActive([MarshalAs(UnmanagedType.Bool)] bool fLock);

    [PreserveSig]
    int GetExtendedControl([MarshalAs(UnmanagedType.IDispatch)] out object? ppDisp);

    [PreserveSig]
    int TransformCoords(ref POINT pPtlHimetric, ref POINT pPtfContainer, uint dwFlags);

    [PreserveSig]
    int TranslateAccelerator(ref MSG pMsg, uint grfModifiers);

    [PreserveSig]
    int OnFocus([MarshalAs(UnmanagedType.Bool)] bool fGotFocus);

    [PreserveSig]
    int ShowPropertyFrame();
}

[ComImport]
[Guid("00000118-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IOleClientSite
{
    [PreserveSig]
    int SaveObject();

    [PreserveSig]
    int GetMoniker(int dwAssign, int dwWhichMoniker, out IntPtr ppmk);

    [PreserveSig]
    int GetContainer(out IntPtr ppContainer);

    [PreserveSig]
    int ShowObject();

    [PreserveSig]
    int OnShowWindow([MarshalAs(UnmanagedType.Bool)] bool fShow);

    [PreserveSig]
    int RequestNewObjectLayout();
}

[ComImport]
[Guid("00000119-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IOleInPlaceSite
{
    [PreserveSig]
    int GetWindow(out IntPtr phwnd);

    [PreserveSig]
    int ContextSensitiveHelp([MarshalAs(UnmanagedType.Bool)] bool fEnterMode);

    [PreserveSig]
    int CanInPlaceActivate();

    [PreserveSig]
    int OnInPlaceActivate();

    [PreserveSig]
    int OnUIActivate();

    [PreserveSig]
    int GetWindowContext(
        out IntPtr ppFrame,
        out IntPtr ppDoc,
        out COMRECT lprcPosRect,
        out COMRECT lprcClipRect,
        ref OLEINPLACEFRAMEINFO lpFrameInfo);

    [PreserveSig]
    int Scroll(SIZEL scrollExtant);

    [PreserveSig]
    int OnUIDeactivate([MarshalAs(UnmanagedType.Bool)] bool fUndoable);

    [PreserveSig]
    int OnInPlaceDeactivate();

    [PreserveSig]
    int DiscardUndoState();

    [PreserveSig]
    int DeactivateAndUndo();

    [PreserveSig]
    int OnPosRectChange(ref COMRECT lprcPosRect);
}

[ComImport]
[Guid("00000112-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IOleObject
{
    [PreserveSig]
    int SetClientSite([In, MarshalAs(UnmanagedType.Interface)] IOleClientSite? pClientSite);

    [PreserveSig]
    int GetClientSite(out IOleClientSite ppClientSite);

    [PreserveSig]
    int SetHostNames(
        [MarshalAs(UnmanagedType.LPWStr)] string szContainerApp,
        [MarshalAs(UnmanagedType.LPWStr)] string szContainerObj);

    [PreserveSig]
    int Close(int dwSaveOption);

    [PreserveSig]
    int SetMoniker(int dwWhichMoniker, IntPtr pmk);

    [PreserveSig]
    int GetMoniker(int dwAssign, int dwWhichMoniker, out IntPtr ppmk);

    [PreserveSig]
    int InitFromData(IntPtr pDataObject, [MarshalAs(UnmanagedType.Bool)] bool fCreation, int dwReserved);

    [PreserveSig]
    int GetClipboardData(int dwReserved, out IntPtr ppDataObject);

    [PreserveSig]
    int DoVerb(
        int iVerb,
        IntPtr lpmsg,
        [In, MarshalAs(UnmanagedType.Interface)] IOleClientSite? pActiveSite,
        int lindex,
        IntPtr hwndParent,
        ref COMRECT lprcPosRect);

    [PreserveSig]
    int EnumVerbs(out IntPtr ppEnumOleVerb);

    [PreserveSig]
    int Update();

    [PreserveSig]
    int IsUpToDate();

    [PreserveSig]
    int GetUserClassID(out Guid pClsid);

    [PreserveSig]
    int GetUserType(int dwFormOfType, [MarshalAs(UnmanagedType.LPWStr)] out string pszUserType);

    [PreserveSig]
    int SetExtent(int dwDrawAspect, ref SIZEL psizel);

    [PreserveSig]
    int GetExtent(int dwDrawAspect, out SIZEL psizel);

    [PreserveSig]
    int Advise(IntPtr pAdvSink, out int pdwConnection);

    [PreserveSig]
    int Unadvise(int dwConnection);

    [PreserveSig]
    int EnumAdvise(out IntPtr ppenumAdvise);

    [PreserveSig]
    int GetMiscStatus(int dwAspect, out int pdwStatus);

    [PreserveSig]
    int SetColorScheme(IntPtr pLogpal);
}

[ComImport]
[Guid("00000113-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IOleInPlaceObject
{
    [PreserveSig]
    int GetWindow(out IntPtr phwnd);

    [PreserveSig]
    int ContextSensitiveHelp([MarshalAs(UnmanagedType.Bool)] bool fEnterMode);

    [PreserveSig]
    int InPlaceDeactivate();

    [PreserveSig]
    int UIDeactivate();

    [PreserveSig]
    int SetObjectRects(ref COMRECT lprcPosRect, ref COMRECT lprcClipRect);

    [PreserveSig]
    int ReactivateAndUndo();
}

[ComImport]
[Guid("7FD52380-4E07-101B-AE2D-08002B2EC713")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IPersistStreamInit
{
    [PreserveSig]
    int GetClassID(out Guid pClassID);

    [PreserveSig]
    int IsDirty();

    [PreserveSig]
    int Load(IntPtr pStm);

    [PreserveSig]
    int Save(IntPtr pStm, [MarshalAs(UnmanagedType.Bool)] bool fClearDirty);

    [PreserveSig]
    int GetSizeMax(out long pcbSize);

    [PreserveSig]
    int InitNew();
}

[ComImport]
[Guid("B196B28D-BAB4-101A-B69C-00AA00341D07")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface ISpecifyPropertyPages
{
    [PreserveSig]
    int GetPages(out CAUUID pPages);
}

[ComImport]
[Guid("3127CA40-446E-11CE-8135-00AA004BB851")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IErrorLog
{
    [PreserveSig]
    int AddError(
        [In, MarshalAs(UnmanagedType.LPWStr)] string pszPropName,
        IntPtr pExcepInfo);
}

[ComImport]
[Guid("55272A00-42CB-11CE-8135-00AA004BB851")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IPropertyBag
{
    [PreserveSig]
    int Read(
        [In, MarshalAs(UnmanagedType.LPWStr)] string pszPropName,
        [In, Out, MarshalAs(UnmanagedType.Struct)] ref object pVar,
        [In, MarshalAs(UnmanagedType.Interface)] IErrorLog? pErrorLog);

    [PreserveSig]
    int Write(
        [In, MarshalAs(UnmanagedType.LPWStr)] string pszPropName,
        [In, MarshalAs(UnmanagedType.Struct)] ref object pVar);
}

[ComImport]
[Guid("37D84F60-42CB-11CE-8135-00AA004BB851")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IPersistPropertyBag
{
    [PreserveSig]
    int GetClassID(out Guid pClassID);

    [PreserveSig]
    int InitNew();

    [PreserveSig]
    int Load(
        [In, MarshalAs(UnmanagedType.Interface)] IPropertyBag pPropBag,
        [In, MarshalAs(UnmanagedType.Interface)] IErrorLog? pErrorLog);

    [PreserveSig]
    int Save(
        [In, MarshalAs(UnmanagedType.Interface)] IPropertyBag pPropBag,
        [In, MarshalAs(UnmanagedType.Bool)] bool fClearDirty,
        [In, MarshalAs(UnmanagedType.Bool)] bool fSaveAllProperties);
}

public delegate IntPtr SubclassProc(
    IntPtr hWnd,
    uint uMsg,
    IntPtr wParam,
    IntPtr lParam,
    UIntPtr uIdSubclass,
    UIntPtr dwRefData);

public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

public delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

public static class Ole32Interop
{
    public const uint WM_CLOSE = 0x0010;
    public const uint WM_ERASEBKGND = 0x0014;
    public const uint WM_SETCURSOR = 0x0020;
    public const uint WM_CONTEXTMENU = 0x007B;
    public const uint WM_TIMER = 0x0113;
    public const uint WM_MOUSEMOVE = 0x0200;
    public const uint WM_LBUTTONDOWN = 0x0201;
    public const uint WM_LBUTTONUP = 0x0202;
    public const uint WM_LBUTTONDBLCLK = 0x0203;
    public const uint WM_RBUTTONDOWN = 0x0204;
    public const uint WM_RBUTTONUP = 0x0205;
    public const uint WM_PRINT = 0x0317;

    public const int PRF_CHECKVISIBLE = 0x01;
    public const int PRF_NONCLIENT = 0x02;
    public const int PRF_CLIENT = 0x04;
    public const int PRF_ERASEBKGND = 0x08;
    public const int PRF_CHILDREN = 0x10;

    public const int DVASPECT_CONTENT = 1;
    public const int OLEIVERB_INPLACEACTIVATE = -5;
    public const int OLECLOSE_NOSAVE = 1;
    public const uint CLSCTX_INPROC_SERVER = 0x1;
    public const uint CLSCTX_LOCAL_SERVER = 0x4;

    public const uint WS_POPUP = 0x80000000;
    public const uint WS_CLIPCHILDREN = 0x02000000;
    public const uint WS_CLIPSIBLINGS = 0x04000000;
    public const uint WS_EX_TOOLWINDOW = 0x00000080;
    public const uint WS_EX_NOACTIVATE = 0x08000000;

    public static readonly Guid IID_IUnknown = new("00000000-0000-0000-C000-000000000046");

    public const int WH_CBT = 5;
    public const int HCBT_CREATEWND = 3;
    public const int HCBT_ACTIVATE = 5;

    private const uint _MCW_EM = 0x0008001F;

    [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern uint _control87(uint newCtrl, uint mask);

    /// <summary>
    /// Enmascara todas las excepciones de punto flotante x87/SSE (división por cero, overflow, underflow)
    /// para evitar que controles OCX nativos de 32 bits generen fallos de hardware en modo diseño.
    /// </summary>
    public static void MaskFpuExceptions()
    {
        try
        {
            _control87(_MCW_EM, _MCW_EM);
        }
        catch
        {
            // Ignorar si no aplica
        }
    }

    [DllImport("ole32.dll", PreserveSig = true)]
    public static extern int CoCreateInstance(
        ref Guid rclsid,
        IntPtr pUnkOuter,
        uint dwClsContext,
        ref Guid riid,
        [MarshalAs(UnmanagedType.IUnknown)] out object? ppv);

    [DllImport("ole32.dll", PreserveSig = true)]
    public static extern int OleDraw(
        [MarshalAs(UnmanagedType.IUnknown)] object pUnk,
        int dwAspect,
        IntPtr hdcDraw,
        ref COMRECT lprcBounds);

    [DllImport("oleaut32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    public static extern int OleCreatePropertyFrame(
        IntPtr hwndOwner,
        int x,
        int y,
        string lpszCaption,
        int cObjects,
        [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.IUnknown)]
        object[] lplpUnk,
        int cPages,
        IntPtr lpPageClsID,
        int lcid,
        int dwReserved,
        IntPtr lpvReserved);

    [DllImport("ole32.dll")]
    public static extern void CoTaskMemFree(IntPtr pv);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DeleteObject(IntPtr hObject);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr CreateWindowExW(
        uint dwExStyle,
        string lpClassName,
        string lpWindowName,
        uint dwStyle,
        int x,
        int y,
        int nWidth,
        int nHeight,
        IntPtr hWndParent,
        IntPtr hMenu,
        IntPtr hInstance,
        IntPtr lpParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern IntPtr SendMessageW(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

    [DllImport("comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetWindowSubclass(
        IntPtr hWnd,
        SubclassProc pfnSubclass,
        UIntPtr uIdSubclass,
        UIntPtr dwRefData);

    [DllImport("comctl32.dll")]
    public static extern IntPtr DefSubclassProc(
        IntPtr hWnd,
        uint uMsg,
        IntPtr wParam,
        IntPtr lParam);

    [DllImport("comctl32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool RemoveWindowSubclass(
        IntPtr hWnd,
        SubclassProc pfnSubclass,
        UIntPtr uIdSubclass);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumChildWindows(
        IntPtr hWndParent,
        EnumWindowsProc lpEnumFunc,
        IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern IntPtr SetCursor(IntPtr hCursor);

    [DllImport("user32.dll")]
    public static extern IntPtr LoadCursor(IntPtr hInstance, int lpCursorName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr SetWindowsHookExW(
        int idHook,
        HookProc lpfn,
        IntPtr hMod,
        uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    public static extern uint GetCurrentThreadId();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetClassNameW(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    public const int GWL_STYLE = -16;
    public const uint WS_CHILD = 0x40000000;
    public const uint WM_COMMAND = 0x0111;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    public static extern int GetWindowLongW(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EndDialog(IntPtr hDlg, IntPtr nResult);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool PostMessageW(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    public const int IDC_ARROW = 32512;
}

/// <summary>
/// Guardián de hilo local (WH_CBT) que intercepta y suprime cualquier cuadro de diálogo de error modal (#32770)
/// disparado por msvbvm60.dll o controles OCX de terceros durante la instanciación o redimensionamiento en modo diseño
/// (por ejemplo, "Run-time error '11': Division by zero" o alertas de licencia en controles no inicializados).
/// Nunca aborta la creación de ventanas hijas de clase #32770 usadas por controles compuestos de MFC/Delphi.
/// </summary>
public sealed class NativeDialogGuard : IDisposable
{
    private IntPtr _hookHandle;
    private readonly HookProc _hookProc;

    public bool DialogSuppressed { get; private set; }

    public NativeDialogGuard()
    {
        Ole32Interop.MaskFpuExceptions();
        _hookProc = CbtHookCallback;
        uint threadId = Ole32Interop.GetCurrentThreadId();
        _hookHandle = Ole32Interop.SetWindowsHookExW(Ole32Interop.WH_CBT, _hookProc, IntPtr.Zero, threadId);
    }

    private IntPtr CbtHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode == Ole32Interop.HCBT_ACTIVATE && wParam != IntPtr.Zero)
        {
            var sb = new StringBuilder(64);
            int len = Ole32Interop.GetClassNameW(wParam, sb, sb.Capacity);
            if (len > 0 && sb.ToString() == "#32770")
            {
                int style = Ole32Interop.GetWindowLongW(wParam, Ole32Interop.GWL_STYLE);
                // Solo suprimir si es un cuadro de diálogo modal superior (no una ventana hija integrada en el control)
                if ((style & (int)Ole32Interop.WS_CHILD) == 0)
                {
                    DialogSuppressed = true;
                    Ole32Interop.EndDialog(wParam, (IntPtr)1);
                    Ole32Interop.PostMessageW(wParam, Ole32Interop.WM_COMMAND, (IntPtr)1, IntPtr.Zero);
                    Ole32Interop.PostMessageW(wParam, Ole32Interop.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                    return (IntPtr)1;
                }
            }
        }

        return Ole32Interop.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            Ole32Interop.UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }
        Ole32Interop.MaskFpuExceptions();
    }
}
