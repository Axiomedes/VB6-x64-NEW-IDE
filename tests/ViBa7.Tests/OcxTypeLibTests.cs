using System;
using System.IO;
using System.Linq;
using ViBa7.ProjectModel.ActiveX;
using ViBa7.ProjectModel.Designers;
using ViBa7.ProjectModel.IntelliSense;
using ViBa7.ProjectModel.Vbp;
using Xunit;

namespace ViBa7.Tests;

public class OcxTypeLibTests
{
    private const string RealOcxPath = @"D:\VisualBasic\AxioUK-AxJControls\ocx\AxJControls.ocx";

    [Fact]
    public void VbControlMemberCatalog_RegistersAndUnregisters_OcxLibrary()
    {
        // Crear biblioteca OCX sintética para prueba independiente de disco
        var lib = new OcxLibraryInfo
        {
            LibraryName = "TestOcxLib",
            FilePath = @"C:\Fake\TestOcxLib.ocx",
            Guid = "{11111111-2222-3333-4444-555555555555}",
            MajorVersion = 1,
            MinorVersion = 0
        };

        var ctrl = new OcxControlInfo
        {
            Name = "FancyButton",
            Description = "Fancy Button OCX Control",
            IsControl = true
        };

        ctrl.Members.Add(new OcxMemberInfo
        {
            Name = "Flash",
            Kind = VbSymbolKind.Method,
            ReturnType = "Void",
            Description = "Flashes the button",
            Parameters =
            [
                new OcxParameterInfo { Name = "Count", Type = "Long", IsOptional = false }
            ]
        });

        ctrl.Members.Add(new OcxMemberInfo
        {
            Name = "GlowColor",
            Kind = VbSymbolKind.Property,
            ReturnType = "Long",
            Description = "Color of the glow effect"
        });

        ctrl.Events.Add(new OcxEventInfo
        {
            Name = "FlashingCompleted",
            Description = "Triggered when flashing finishes"
        });

        lib.Controls.Add(ctrl);

        // Registro en el catálogo
        VbControlMemberCatalog.RegisterOcxLibrary(lib);

        Assert.True(VbControlMemberCatalog.IsControlRegistered("FancyButton"));
        Assert.True(VbControlMemberCatalog.IsControlRegistered("TestOcxLib.FancyButton"));

        var members = VbControlMemberCatalog.GetMembersForControl("FancyButton");
        Assert.NotEmpty(members);
        Assert.Contains(members, m => m.Name == "Flash" && m.Kind == VbSymbolKind.Method);
        Assert.Contains(members, m => m.Name == "GlowColor" && m.Kind == VbSymbolKind.Property);
        Assert.Contains(members, m => m.Name == "FlashingCompleted" && m.Kind == VbSymbolKind.Event);

        var flashMethod = VbControlMemberCatalog.FindMember("FancyButton", "Flash");
        Assert.NotNull(flashMethod);
        Assert.Single(flashMethod.Parameters);
        Assert.Equal("Count", flashMethod.Parameters[0].Name);

        // Desregistro
        VbControlMemberCatalog.UnregisterOcxLibrary("TestOcxLib");
        Assert.False(VbControlMemberCatalog.IsControlRegistered("FancyButton"));
        Assert.Empty(VbControlMemberCatalog.GetMembersForControl("FancyButton"));
    }

    [Fact]
    public void ControlToolboxRegistry_RegistersAndUnregisters_OcxTools()
    {
        var lib = new OcxLibraryInfo
        {
            LibraryName = "GridLib",
            FilePath = @"C:\Fake\GridLib.ocx"
        };

        lib.Controls.Add(new OcxControlInfo
        {
            Name = "SuperGrid",
            Description = "Super Data Grid",
            IsControl = true
        });

        ControlToolboxRegistry.RegisterOcxTools(lib);

        var tool = ControlToolboxRegistry.Find("GridLib.SuperGrid");
        Assert.NotNull(tool);
        Assert.Equal("SuperGrid", tool.DisplayName);
        Assert.Equal("GridLib.SuperGrid", tool.TypeName);

        // Instanciar control
        var model = new FormDesignModel { Name = "Form1" };
        var created = ControlToolboxRegistry.CreateControl("GridLib.SuperGrid", model, 200, 300);
        Assert.NotNull(created);
        Assert.Equal("GridLib.SuperGrid", created.TypeName);
        Assert.StartsWith("SuperGrid", created.Name);

        ControlToolboxRegistry.UnregisterOcxTools("GridLib");
        Assert.Null(ControlToolboxRegistry.Find("GridLib.SuperGrid"));
    }

    [Fact]
    public void IntelliSenseContextDetector_ProvidesMemberAccess_ForOcxControl()
    {
        var lib = new OcxLibraryInfo
        {
            LibraryName = "CustomControls",
            FilePath = @"C:\Fake\CustomControls.ocx"
        };

        var ctrl = new OcxControlInfo { Name = "axFancyList", IsControl = true };
        ctrl.Members.Add(new OcxMemberInfo { Name = "AddItem", Kind = VbSymbolKind.Method, ReturnType = "Void" });
        ctrl.Members.Add(new OcxMemberInfo { Name = "Clear", Kind = VbSymbolKind.Method, ReturnType = "Void" });
        ctrl.Members.Add(new OcxMemberInfo { Name = "ItemCount", Kind = VbSymbolKind.Property, ReturnType = "Long" });
        lib.Controls.Add(ctrl);

        VbControlMemberCatalog.RegisterOcxLibrary(lib);

        try
        {
            var formModel = new FormDesignModel { Name = "Form1" };
            formModel.Children.Add(new FormDesignerControl
            {
                Name = "axFancyList1",
                TypeName = "CustomControls.axFancyList"
            });

            string code = "Sub Test()\r\n    axFancyList1.\r\nEnd Sub";
            int dotOffset = code.IndexOf('.') + 1;

            var ctx = IntelliSenseContextDetector.DetectContext(code, dotOffset, formModel, explicitTrigger: true);

            Assert.Equal(IntelliSenseContextKind.MemberAccess, ctx.Kind);
            Assert.Equal("axFancyList1", ctx.TargetExpression);
            Assert.Contains(ctx.Candidates, c => c.Name == "AddItem");
            Assert.Contains(ctx.Candidates, c => c.Name == "Clear");
            Assert.Contains(ctx.Candidates, c => c.Name == "ItemCount");
        }
        finally
        {
            VbControlMemberCatalog.UnregisterOcxLibrary("CustomControls");
        }
    }

    [Fact]
    public void RealOcxFile_ReadTypeLib_ExtractsExpectedMetadata_IfAvailable()
    {
        if (!OperatingSystem.IsWindows() || !File.Exists(RealOcxPath))
        {
            return; // Omitir si se ejecuta en una máquina donde no está presente este archivo específico
        }

        var libInfo = OcxTypeLibReader.ReadTypeLib(RealOcxPath);

        Assert.NotNull(libInfo);
        Assert.Equal("AxJControls", libInfo.LibraryName);
        Assert.NotEmpty(libInfo.Controls);

        // Debe contener axJColCombo, axJCombo, axJList
        Assert.Contains(libInfo.Controls, c => c.Name == "axJColCombo");
        Assert.Contains(libInfo.Controls, c => c.Name == "axJCombo");
        Assert.Contains(libInfo.Controls, c => c.Name == "axJList");

        var colCombo = libInfo.Controls.First(c => c.Name == "axJColCombo");
        Assert.NotEmpty(colCombo.Members);

        // Métodos y propiedades esperadas
        var memberNames = colCombo.Members.Select(m => m.Name).ToList();
        Assert.Contains("AddItem", memberNames);
        Assert.Contains("RemoveItem", memberNames);
        Assert.Contains("Text", memberNames);
        Assert.Contains("BackColor", memberNames);

        // Parámetros de AddItem
        var addItem = colCombo.Members.First(m => m.Name == "AddItem");
        Assert.NotEmpty(addItem.Parameters);

        // Enumeraciones
        Assert.NotEmpty(libInfo.Enums);
        Assert.Contains(libInfo.Enums, e => e.Name == "JComboShapes");
    }

    [Fact]
    public void VB6Project_AddActiveXControl_UpdatesItemsAndRawLines()
    {
        var project = new VB6Project { Name = "Project1" };
        var lib = new OcxLibraryInfo
        {
            LibraryName = "AxJControls",
            FilePath = @"D:\VisualBasic\AxioUK-AxJControls\ocx\AxJControls.ocx",
            Guid = "{4BD86386-E0DF-4D05-9268-F975619A0A10}",
            MajorVersion = 1,
            MinorVersion = 0
        };

        project.AddActiveXControl(lib, "AxJControls.ocx");

        Assert.Single(project.ActiveXControls);
        var item = project.ActiveXControls.First();
        Assert.Equal("AxJControls", item.Name);
        Assert.Equal("AxJControls.ocx", item.RelativePath);
        Assert.Contains("Object={4BD86386-E0DF-4D05-9268-F975619A0A10}#1.0#0; AxJControls.ocx", item.RawLine);
        Assert.Contains(item.RawLine, project.RawLines);
        Assert.Contains(lib, project.LoadedOcxLibraries);
    }

    [Fact]
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public void SystemActiveXRegistryScanner_EnumeratesRegisteredComponents_Successfully()
    {
        var components = SystemActiveXRegistryScanner.GetRegisteredComponents();
        Assert.NotNull(components);
        Assert.NotEmpty(components);

        // Al menos una TypeLib común del sistema operativo debe estar presente (e.g. OLE Automation / stdole2)
        Assert.Contains(components, c => !string.IsNullOrEmpty(c.FilePath));
    }

    [Fact]
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public void SystemActiveXRegistryScanner_DistinguishesControlsAndDlls()
    {
        var components = SystemActiveXRegistryScanner.GetRegisteredComponents();
        var ocxComponents = components.Where(c => c.FileExtension.Equals(".ocx", StringComparison.OrdinalIgnoreCase)).ToList();

        foreach (var ocx in ocxComponents)
        {
            Assert.True(ocx.IsControl, $"El archivo {ocx.FilePath} es un .ocx y debe tener IsControl=true");
        }
    }

    [Fact]
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public void SystemActiveXRegistryScanner_HandlesRegisterActiveXFile_NonExistentFile()
    {
        bool result = SystemActiveXRegistryScanner.RegisterActiveXFileInSystem(@"C:\Inexistente\ControlFantasma.ocx", out string? msg);
        Assert.False(result);
        Assert.NotNull(msg);
        Assert.Contains("no existe", msg, StringComparison.OrdinalIgnoreCase);
    }
}
