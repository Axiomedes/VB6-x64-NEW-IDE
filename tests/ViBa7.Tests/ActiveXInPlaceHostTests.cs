using System;
using System.Collections.Generic;
using ViBa7.ActiveXHost;
using ViBa7.ProjectModel.Designers;
using Xunit;

namespace ViBa7.Tests;

public class ActiveXInPlaceHostTests
{
    [Fact]
    public void ActiveXHostFactory_TryResolveClsid_KnownControls()
    {
        Assert.True(ActiveXHostFactory.TryResolveClsid("MSFlexGridLib.MSFlexGrid", out string flexClsid));
        Assert.Equal("{6262D3A0-5316-11CE-BFC1-08002BE10318}", flexClsid);

        Assert.True(ActiveXHostFactory.TryResolveClsid("MSComctlLib.TreeCtrl", out string treeClsid));
        Assert.Equal("{C74190B6-8589-11D1-B16A-00C0F0283628}", treeClsid);

        Assert.True(ActiveXHostFactory.TryResolveClsid("RichTextLib.RichTextBox", out string rtfClsid));
        Assert.Equal("{3B7C8860-D78F-101B-B9B5-04021C009402}", rtfClsid);

        Assert.True(ActiveXHostFactory.TryResolveClsid("TabDlg.SSTab", out string ssTabClsid));
        Assert.Equal("{BDC217C8-ED16-11CD-956C-0000C04E4C0A}", ssTabClsid);

        Assert.True(ActiveXHostFactory.TryResolveClsid("MSComDlg.CommonDialog", out string dlgClsid));
        Assert.Equal("{F9043C88-F6F2-101A-A3C9-08002B2F49FB}", dlgClsid);
    }

    [Fact]
    public void ActiveXHostFactory_TryResolveClsid_ExplicitGuid()
    {
        string rawGuid = "{12345678-ABCD-EF01-2345-6789ABCDEF01}";
        Assert.True(ActiveXHostFactory.TryResolveClsid(rawGuid, out string resolved));
        Assert.Equal(rawGuid, resolved);
    }

    [Fact]
    public void ActiveXHostFactory_TryResolveClsid_InvalidReturnsFalse()
    {
        Assert.False(ActiveXHostFactory.TryResolveClsid("NotARealControl.NonExistentClsidXYZ123", out _));
        Assert.False(ActiveXHostFactory.TryResolveClsid("", out _));
    }

    [Fact]
    public void ActiveXHostFactory_TryCreateHost_IntrinsicControls_ReturnsNull()
    {
        var btn = new FormDesignerControl { TypeName = "VB.CommandButton", Name = "Command1" };
        var txt = new FormDesignerControl { TypeName = "VB.TextBox", Name = "Text1" };
        var lbl = new FormDesignerControl { TypeName = "VB.Label", Name = "Label1" };

        Assert.Null(ActiveXHostFactory.TryCreateHost(btn));
        Assert.Null(ActiveXHostFactory.TryCreateHost(txt));
        Assert.Null(ActiveXHostFactory.TryCreateHost(lbl));
    }

    [Fact]
    public void ActiveXHostFactory_EnableInPlaceActiveXHosting_CanToggle()
    {
        bool original = ActiveXHostFactory.EnableInPlaceActiveXHosting;
        try
        {
            ActiveXHostFactory.EnableInPlaceActiveXHosting = false;
            var flex = new FormDesignerControl { TypeName = "MSFlexGridLib.MSFlexGrid", Name = "MSFlexGrid1" };
            var host = ActiveXHostFactory.TryCreateHost(flex);
            Assert.Null(host);
        }
        finally
        {
            ActiveXHostFactory.EnableInPlaceActiveXHosting = original;
        }
    }

    private static void RunInSta(Action action)
    {
        Exception? exception = null;
        var thread = new System.Threading.Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });
        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception != null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }

    [Fact]
    public void DynamicActiveXControl_NormalizesClsid()
    {
        RunInSta(() =>
        {
            // Un GUID sin llaves debe normalizarse con llaves
            string raw = "6262D3A0-5316-11CE-BFC1-08002BE10318";
            var ctrl = new DynamicActiveXControl(raw);
            Assert.Equal("{6262D3A0-5316-11CE-BFC1-08002BE10318}", ctrl.Clsid);
            Assert.True(ctrl.IsDesignMode);
        });
    }

    [Fact]
    public void ActiveXInPlaceHost_InitializationAndPropertySync()
    {
        RunInSta(() =>
        {
            var modelControl = new FormDesignerControl
            {
                TypeName = "MSFlexGridLib.MSFlexGrid",
                Name = "Grid1",
                Left = 120,
                Top = 240,
                Width = 3000,
                Height = 2000
            };
            modelControl.CustomProperties.Add(new FormPropertyItem("Cols", "5"));
            modelControl.CustomProperties.Add(new FormPropertyItem("Rows", "10"));

            var host = new ActiveXInPlaceHost("{6262D3A0-5316-11CE-BFC1-08002BE10318}", "MSFlexGridLib.MSFlexGrid")
            {
                BoundControl = modelControl,
                Width = modelControl.WidthPixels,
                Height = modelControl.HeightPixels
            };

            Assert.Equal("{6262D3A0-5316-11CE-BFC1-08002BE10318}", host.Clsid);
            Assert.Equal("MSFlexGridLib.MSFlexGrid", host.ProgId);
            Assert.Same(modelControl, host.BoundControl);
            Assert.True(host.IsDesignMode);
            Assert.Equal(200.0, host.Width); // 3000 / 15
            Assert.Equal(133.33333333333334, host.Height); // 2000 / 15

            // Validar que SyncFromDesignerModel no genere excepciones aún sin instanciar la ventana Win32 en pruebas headless
            host.SyncFromDesignerModel();
            host.Dispose();
        });
    }

    [Fact]
    public void ActiveXInPlaceHost_FallbackVisual_AutomaticWhenNativeNotRendered()
    {
        RunInSta(() =>
        {
            var modelControl = new FormDesignerControl
            {
                TypeName = "ThirdPartyLib.CustomGrid",
                Name = "CustomGrid1",
                Width = 2400,
                Height = 1500
            };

            var fallbackBorder = new System.Windows.Controls.Border
            {
                Width = 160,
                Height = 100
            };

            // CLSID ficticio que no existe en el registro: TryInitializeAndRender fallará limpiamente y activará FallbackVisual
            using var host = new ActiveXInPlaceHost("{99999999-8888-7777-6666-555555555555}", "ThirdPartyLib.CustomGrid", 160, 100)
            {
                BoundControl = modelControl,
                FallbackVisual = fallbackBorder
            };

            bool nativeOk = host.TryInitializeAndRender();
            Assert.False(nativeOk);
            Assert.False(host.HasValidNativeVisual);
            Assert.Same(fallbackBorder, host.Child);
        });
    }

    [Fact]
    public void DynamicActiveXControl_ClampsZeroDimensionsAndSetsDesignModeSite()
    {
        RunInSta(() =>
        {
            using var host = new ActiveXInPlaceHost("{6262D3A0-5316-11CE-BFC1-08002BE10318}", "MSFlexGridLib.MSFlexGrid", 0, 0);
            Assert.True(host.AxControl.Width >= 16);
            Assert.True(host.AxControl.Height >= 16);
            Assert.NotNull(host.DesignSite);
            Assert.True(host.DesignSite.DesignMode);
        });
    }

    [Fact]
    public void VbPropertyBag_ReturnsS_OkForDefinedAndE_InvalidArgForMissing()
    {
        var props = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["Cols"] = 4,
            ["Caption"] = "Título",
            ["Enabled"] = true
        };

        var bag = new VbPropertyBag(props);

        object colsVal = 0;
        int hrCols = bag.Read("Cols", ref colsVal, null);
        Assert.Equal(0, hrCols); // S_OK
        Assert.Equal(4, colsVal);

        object capVal = string.Empty;
        int hrCap = bag.Read("Caption", ref capVal, null);
        Assert.Equal(0, hrCap); // S_OK
        Assert.Equal("Título", capVal);

        // Propiedad no definida en el .frm: debe retornar E_INVALIDARG (0x80070057) para que msvbvm60.dll use el DefaultValue del autor del OCX
        object missingVal = 100;
        int hrMissing = bag.Read("UnsetDivisorProperty", ref missingVal, null);
        Assert.Equal(unchecked((int)0x80070057), hrMissing);
        Assert.Equal(100, missingVal);
    }

    [Fact]
    public void VbControlMemberCatalog_TryResolveOcxClsid_ResolvesFromRegisteredTypeLib()
    {
        var dummyLib = new ViBa7.ProjectModel.ActiveX.OcxLibraryInfo
        {
            LibraryName = "JControlsLib",
            FilePath = @"C:\Dummy\axJControls.ocx",
            Guid = "{11111111-2222-3333-4444-555555555555}"
        };
        dummyLib.Controls.Add(new ViBa7.ProjectModel.ActiveX.OcxControlInfo
        {
            Name = "axJColCombo",
            Clsid = "{AABBCCDD-1122-3344-5566-77889900AABB}",
            IsControl = true
        });

        ViBa7.ProjectModel.IntelliSense.VbControlMemberCatalog.RegisterOcxLibrary(dummyLib);
        try
        {
            Assert.True(ActiveXHostFactory.TryResolveClsid("JControlsLib.axJColCombo", out string clsid1));
            Assert.Equal("{AABBCCDD-1122-3344-5566-77889900AABB}", clsid1);

            Assert.True(ActiveXHostFactory.TryResolveClsid("axJControls.axJColCombo", out string clsid2));
            Assert.Equal("{AABBCCDD-1122-3344-5566-77889900AABB}", clsid2);
        }
        finally
        {
            ViBa7.ProjectModel.IntelliSense.VbControlMemberCatalog.UnregisterOcxLibrary("JControlsLib");
        }
    }

    [Fact]
    public void ActiveXHostFactory_TryCreateHost_NonVisualControls_ReturnsNull()
    {
        var dlg = new FormDesignerControl { TypeName = "MSComDlg.CommonDialog", Name = "CommonDialog1" };
        var imgList = new FormDesignerControl { TypeName = "MSComctlLib.ImageListCtrl", Name = "ImageList1" };

        Assert.Null(ActiveXHostFactory.TryCreateHost(dlg));
        Assert.Null(ActiveXHostFactory.TryCreateHost(imgList));
    }

    [Fact]
    public void NativeDialogGuard_And_MaskFpuExceptions_ExecuteCleanly()
    {
        RunInSta(() =>
        {
            ViBa7.ActiveXHost.Native.Ole32Interop.MaskFpuExceptions();
            using var guard = new ViBa7.ActiveXHost.Native.NativeDialogGuard();
            Assert.False(guard.DialogSuppressed);
        });
    }

    [Fact]
    public void DynamicActiveXControl_IsIndependentOfWinFormsAxHost_SupportsWindowlessControls()
    {
        RunInSta(() =>
        {
            using var ctrl = new DynamicActiveXControl("{6262D3A0-5316-11CE-BFC1-08002BE10318}")
            {
                IsDesignMode = true,
                Width = 120,
                Height = 40
            };

            // Verificar que DynamicActiveXControl NO hereda de System.Windows.Forms.AxHost ni Control,
            // evitando System.NotSupportedException en AxHost.EnsureWindowPresent() para controles OCX windowless.
            Assert.False(typeof(System.Windows.Forms.Control).IsAssignableFrom(typeof(DynamicActiveXControl)));

            var site = new OleDesignClientSite(ctrl);
            Assert.False(site.UserMode); // DesignMode = True -> UserMode = False
            Assert.False(site.UIDead); // UIDead debe ser False para que los controles no queden bloqueados o deshabilitados
            Assert.Equal("Twip", site.ScaleUnits);
            Assert.False(site.ShowGrabHandles);
            Assert.False(site.ShowHatching);

            Assert.Equal(0, site.CanInPlaceActivate()); // S_OK
        });
    }

    [Fact]
    public void OleDesignClientSite_ProvidesValidFramePointerAndFrameMethods()
    {
        RunInSta(() =>
        {
            using var ctrl = new DynamicActiveXControl("{6262D3A0-5316-11CE-BFC1-08002BE10318}")
            {
                IsDesignMode = true,
                Width = 200,
                Height = 80
            };

            var site = new OleDesignClientSite(ctrl);
            var frameInfo = new ViBa7.ActiveXHost.Native.OLEINPLACEFRAMEINFO
            {
                cb = System.Runtime.InteropServices.Marshal.SizeOf<ViBa7.ActiveXHost.Native.OLEINPLACEFRAMEINFO>()
            };

            int hr = site.GetWindowContext(out IntPtr ppFrame, out IntPtr ppDoc, out var posRect, out var clipRect, ref frameInfo);
            Assert.Equal(0, hr);
            Assert.NotEqual(IntPtr.Zero, ppFrame);
            Assert.Equal(200, posRect.right);
            Assert.Equal(80, posRect.bottom);

            // Liberar interfaz COM obtenida en GetWindowContext
            System.Runtime.InteropServices.Marshal.Release(ppFrame);
        });
    }

    [Fact]
    public void DynamicActiveXControl_ConvertPropertyStringToOcxType_PreservesEnumZerosAndNegatives()
    {
        // Propiedades de enumeración / enteras con 0 o -1 no deben ser convertidas a booleano
        object? borderVal = DynamicActiveXControl.ConvertPropertyStringToOcxType("BorderStyle", "0");
        Assert.IsType<int>(borderVal);
        Assert.Equal(0, borderVal);

        object? styleVal = DynamicActiveXControl.ConvertPropertyStringToOcxType("Style", "-1");
        Assert.IsType<int>(styleVal);
        Assert.Equal(-1, styleVal);

        // Propiedades conocidas booleanas con 0 o -1 sí deben convertirse a booleano
        object? enabledFalse = DynamicActiveXControl.ConvertPropertyStringToOcxType("Enabled", "0");
        Assert.IsType<bool>(enabledFalse);
        Assert.False((bool)enabledFalse);

        object? visibleTrue = DynamicActiveXControl.ConvertPropertyStringToOcxType("Visible", "-1");
        Assert.IsType<bool>(visibleTrue);
        Assert.True((bool)visibleTrue);
    }

    [Fact]
    public void VbPropertyBag_CoercionAndSavedProperties()
    {
        var initial = new Dictionary<string, object>
        {
            ["Enabled"] = "-1",
            ["BorderStyle"] = 1,
            ["ForeColor"] = "&H00FF0000&"
        };

        var bag = new VbPropertyBag(initial);
        Assert.Equal(3, bag.SavedProperties.Count);

        object pVarBool = false;
        int hrBool = bag.Read("Enabled", ref pVarBool, null);
        Assert.Equal(0, hrBool);
        Assert.True((bool)pVarBool);

        object pVarInt = 0;
        int hrInt = bag.Read("BorderStyle", ref pVarInt, null);
        Assert.Equal(0, hrInt);
        Assert.Equal(1, pVarInt);

        object pVarMissing = null!;
        int hrMissing = bag.Read("NonExistentProperty", ref pVarMissing, null);
        Assert.NotEqual(0, hrMissing); // E_INVALIDARG para usar valor por defecto del control
    }
}

