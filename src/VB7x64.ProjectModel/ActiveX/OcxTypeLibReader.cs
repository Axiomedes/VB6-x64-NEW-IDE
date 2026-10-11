using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;
using Microsoft.Win32;
using VB7x64.ProjectModel.IntelliSense;

namespace VB7x64.ProjectModel.ActiveX;

[Flags]
public enum ComImplTypeFlags
{
    Default = 1,
    Source = 2,
    Restricted = 4,
    DefaultVTable = 8
}

/// <summary>
/// Lector de Type Libraries COM (.ocx, .dll, .tlb) que opera de forma nativa en procesos x64
/// utilizando oleaut32.dll!LoadTypeLibEx con REGKIND_NONE, sin requerir registro COM previo
/// ni permisos de administrador.
/// </summary>
[SupportedOSPlatform("windows")]
public static class OcxTypeLibReader
{
    private const int REGKIND_NONE = 2;

    [DllImport("oleaut32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void LoadTypeLibEx(
        string szFile,
        int regkind,
        out ITypeLib pptlib);

    /// <summary>
    /// Intenta resolver la ruta de un archivo OCX/TLB a partir de su declaración en el proyecto VB6.
    /// </summary>
    public static string? ResolveOcxPath(string relativeOrDeclaredPath, string? projectDirectory = null, string? guid = null)
    {
        if (string.IsNullOrWhiteSpace(relativeOrDeclaredPath)) return null;

        // 1. Ruta absoluta existente
        if (Path.IsPathRooted(relativeOrDeclaredPath) && File.Exists(relativeOrDeclaredPath))
        {
            return Path.GetFullPath(relativeOrDeclaredPath);
        }

        // 2. Ruta relativa al directorio del proyecto
        if (!string.IsNullOrEmpty(projectDirectory))
        {
            string combined = Path.Combine(projectDirectory, relativeOrDeclaredPath);
            if (File.Exists(combined))
            {
                return Path.GetFullPath(combined);
            }
        }

        string fileName = Path.GetFileName(relativeOrDeclaredPath);

        // 3. Directorio de SysWOW64 (controles COM clásicos de 32 bits en Windows x64)
        string sysWow64 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SysWOW64");
        string wow64Path = Path.Combine(sysWow64, fileName);
        if (File.Exists(wow64Path))
        {
            return wow64Path;
        }

        // 4. Directorio de System32
        string sys32 = Environment.SystemDirectory;
        string sys32Path = Path.Combine(sys32, fileName);
        if (File.Exists(sys32Path))
        {
            return sys32Path;
        }

        // 5. Búsqueda en el Registro de Windows bajo HKCR\TypeLib si se dispone del GUID
        if (!string.IsNullOrWhiteSpace(guid))
        {
            try
            {
                string cleanGuid = guid.Trim();
                if (!cleanGuid.StartsWith('{')) cleanGuid = "{" + cleanGuid + "}";

                using var tlibKey = Registry.ClassesRoot.OpenSubKey($@"TypeLib\{cleanGuid}");
                if (tlibKey != null)
                {
                    foreach (var verName in tlibKey.GetSubKeyNames())
                    {
                        using var verKey = tlibKey.OpenSubKey(verName);
                        if (verKey == null) continue;

                        using var win32Key = verKey.OpenSubKey(@"0\win32");
                        if (win32Key?.GetValue(null) is string regPath32 && File.Exists(regPath32))
                        {
                            return regPath32;
                        }

                        using var win64Key = verKey.OpenSubKey(@"0\win64");
                        if (win64Key?.GetValue(null) is string regPath64 && File.Exists(regPath64))
                        {
                            return regPath64;
                        }
                    }
                }
            }
            catch
            {
                // Ignorar excepciones de lectura de registro
            }
        }

        return null;
    }

    /// <summary>
    /// Intenta leer la Type Library de forma segura capturando posibles excepciones COM.
    /// </summary>
    public static bool TryReadTypeLib(string filePath, out OcxLibraryInfo? libInfo, out string? errorMessage)
    {
        libInfo = null;
        errorMessage = null;

        try
        {
            libInfo = ReadTypeLib(filePath);
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Carga la Type Library incrustada en un archivo .ocx, .dll o .tlb y extrae todos los controles,
    /// interfaces, métodos, propiedades, parámetros y enumeraciones.
    /// </summary>
    public static OcxLibraryInfo ReadTypeLib(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"El archivo OCX o TypeLib no existe: {filePath}", filePath);
        }

        LoadTypeLibEx(filePath, REGKIND_NONE, out ITypeLib typeLib);

        var libInfo = new OcxLibraryInfo
        {
            FilePath = filePath
        };

        try
        {
            // Atributos de la librería
            nint pLibAttr = nint.Zero;
            typeLib.GetLibAttr(out pLibAttr);
            if (pLibAttr != nint.Zero)
            {
                try
                {
                    TYPELIBATTR libAttr = Marshal.PtrToStructure<TYPELIBATTR>(pLibAttr);
                    libInfo.Guid = libAttr.guid.ToString("B").ToUpperInvariant();
                    libInfo.MajorVersion = libAttr.wMajorVerNum;
                    libInfo.MinorVersion = libAttr.wMinorVerNum;
                }
                finally
                {
                    typeLib.ReleaseTLibAttr(pLibAttr);
                }
            }

            typeLib.GetDocumentation(-1, out string libName, out string libDoc, out _, out _);
            libInfo.LibraryName = libName ?? Path.GetFileNameWithoutExtension(filePath);
            libInfo.Description = libDoc ?? string.Empty;

            int typeInfoCount = typeLib.GetTypeInfoCount();

            for (int i = 0; i < typeInfoCount; i++)
            {
                typeLib.GetTypeInfo(i, out ITypeInfo tinfo);
                nint pAttr = nint.Zero;
                tinfo.GetTypeAttr(out pAttr);
                if (pAttr == nint.Zero)
                {
                    Marshal.ReleaseComObject(tinfo);
                    continue;
                }

                try
                {
                    TYPEATTR attr = Marshal.PtrToStructure<TYPEATTR>(pAttr);
                    tinfo.GetDocumentation(-1, out string typeName, out string typeDoc, out _, out _);

                    switch (attr.typekind)
                    {
                        case TYPEKIND.TKIND_COCLASS:
                            var ctrl = ProcessCoClass(typeName, typeDoc, tinfo, attr);
                            libInfo.Controls.Add(ctrl);
                            break;

                        case TYPEKIND.TKIND_ENUM:
                            var enumInfo = ExtractEnumInfo(tinfo, attr, typeName, typeDoc);
                            libInfo.Enums.Add(enumInfo);
                            break;
                    }
                }
                finally
                {
                    tinfo.ReleaseTypeAttr(pAttr);
                    Marshal.ReleaseComObject(tinfo);
                }
            }
        }
        finally
        {
            Marshal.ReleaseComObject(typeLib);
        }

        return libInfo;
    }

    private static OcxControlInfo ProcessCoClass(
        string name,
        string doc,
        ITypeInfo coclassInfo,
        TYPEATTR coclassAttr)
    {
        var ctrl = new OcxControlInfo
        {
            Name = name,
            Description = doc ?? string.Empty,
            Clsid = coclassAttr.guid.ToString("B").ToUpperInvariant(),
            IsControl = true
        };

        for (int i = 0; i < coclassAttr.cImplTypes; i++)
        {
            coclassInfo.GetRefTypeOfImplType(i, out int hRefType);
            coclassInfo.GetRefTypeInfo(hRefType, out ITypeInfo implInfo);
            coclassInfo.GetImplTypeFlags(i, out IMPLTYPEFLAGS implFlags);

            nint pImplAttr = nint.Zero;
            implInfo.GetTypeAttr(out pImplAttr);
            if (pImplAttr == nint.Zero)
            {
                Marshal.ReleaseComObject(implInfo);
                continue;
            }

            try
            {
                TYPEATTR implAttr = Marshal.PtrToStructure<TYPEATTR>(pImplAttr);
                implInfo.GetDocumentation(-1, out string implName, out _, out _, out _);

                bool isSource = ((int)implFlags & (int)ComImplTypeFlags.Source) != 0;

                if (isSource)
                {
                    // Interfaz de eventos (e.g. __axJColCombo)
                    ctrl.EventsInterfaceName = implName;
                    ExtractMembersAndEvents(implInfo, implAttr, null, ctrl.Events);
                }
                else
                {
                    // Interfaz principal de propiedades y métodos (e.g. _axJColCombo)
                    ctrl.DefaultInterfaceName = implName;
                    ExtractMembersAndEvents(implInfo, implAttr, ctrl.Members, null);
                }
            }
            finally
            {
                implInfo.ReleaseTypeAttr(pImplAttr);
                Marshal.ReleaseComObject(implInfo);
            }
        }

        return ctrl;
    }

    private static void ExtractMembersAndEvents(
        ITypeInfo tinfo,
        TYPEATTR attr,
        List<OcxMemberInfo>? membersList,
        List<OcxEventInfo>? eventsList)
    {
        var membersByName = new Dictionary<string, OcxMemberInfo>(StringComparer.OrdinalIgnoreCase);

        for (int f = 0; f < attr.cFuncs; f++)
        {
            nint pFuncDesc = nint.Zero;
            tinfo.GetFuncDesc(f, out pFuncDesc);
            if (pFuncDesc == nint.Zero) continue;

            try
            {
                FUNCDESC fd = Marshal.PtrToStructure<FUNCDESC>(pFuncDesc);

                // Obtener nombres de función y parámetros
                string[] names = new string[fd.cParams + 1];
                tinfo.GetNames(fd.memid, names, names.Length, out int cNames);

                string funcName = cNames > 0 ? names[0] : string.Empty;
                if (string.IsNullOrWhiteSpace(funcName)) continue;

                // Ignorar funciones IUnknown / IDispatch estándar
                if (IsStandardComMethod(funcName)) continue;

                tinfo.GetDocumentation(fd.memid, out _, out string funcDoc, out _, out _);

                // Si estamos extrayendo eventos
                if (eventsList != null)
                {
                    var evt = new OcxEventInfo
                    {
                        Name = funcName,
                        Description = funcDoc ?? string.Empty,
                        DispId = fd.memid
                    };

                    ExtractParameters(tinfo, fd, names, evt.Parameters);
                    eventsList.Add(evt);
                    continue;
                }

                // Si estamos extrayendo miembros (propiedades y métodos)
                if (membersList != null)
                {
                    bool isPropGet = fd.invkind == INVOKEKIND.INVOKE_PROPERTYGET;
                    bool isPropPut = fd.invkind is INVOKEKIND.INVOKE_PROPERTYPUT or INVOKEKIND.INVOKE_PROPERTYPUTREF;
                    bool isProperty = isPropGet || isPropPut;

                    if (membersByName.TryGetValue(funcName, out var existing))
                    {
                        // Unificar Property Get y Let/Set
                        if (isPropGet)
                        {
                            existing.IsPropertyGet = true;
                            existing.ReturnType = GetTypeNameFromElemDesc(tinfo, fd.elemdescFunc);
                        }
                        if (isPropPut)
                        {
                            existing.IsPropertyPut = true;
                        }
                        if (string.IsNullOrEmpty(existing.Description) && !string.IsNullOrEmpty(funcDoc))
                        {
                            existing.Description = funcDoc;
                        }
                    }
                    else
                    {
                        var member = new OcxMemberInfo
                        {
                            Name = funcName,
                            Kind = isProperty ? VbSymbolKind.Property : VbSymbolKind.Method,
                            ReturnType = isPropPut ? "Void" : GetTypeNameFromElemDesc(tinfo, fd.elemdescFunc),
                            Description = funcDoc ?? string.Empty,
                            IsPropertyGet = isPropGet,
                            IsPropertyPut = isPropPut,
                            DispId = fd.memid
                        };

                        ExtractParameters(tinfo, fd, names, member.Parameters);
                        membersByName[funcName] = member;
                        membersList.Add(member);
                    }
                }
            }
            finally
            {
                tinfo.ReleaseFuncDesc(pFuncDesc);
            }
        }
    }

    private static void ExtractParameters(
        ITypeInfo tinfo,
        FUNCDESC fd,
        string[] names,
        List<OcxParameterInfo> parameters)
    {
        int paramCount = fd.cParams;
        if (paramCount <= 0 || fd.lprgelemdescParam == nint.Zero) return;

        int elemDescSize = Marshal.SizeOf<ELEMDESC>();

        for (int p = 0; p < paramCount; p++)
        {
            nint pElem = fd.lprgelemdescParam + (p * elemDescSize);
            ELEMDESC elem = Marshal.PtrToStructure<ELEMDESC>(pElem);

            string paramName = (p + 1 < names.Length && !string.IsNullOrEmpty(names[p + 1]))
                ? names[p + 1]
                : $"param{p + 1}";

            string paramType = GetTypeNameFromElemDesc(tinfo, elem);
            bool isOptional = (elem.desc.paramdesc.wParamFlags & PARAMFLAG.PARAMFLAG_FOPT) != 0;

            parameters.Add(new OcxParameterInfo
            {
                Name = paramName,
                Type = paramType,
                IsOptional = isOptional
            });
        }
    }

    private static string GetTypeNameFromElemDesc(ITypeInfo tinfo, ELEMDESC elem)
    {
        return DecodeTypeDesc(tinfo, elem.tdesc);
    }

    private static string DecodeTypeDesc(ITypeInfo tinfo, TYPEDESC tdesc)
    {
        var vt = (VarEnum)tdesc.vt;

        if (vt == VarEnum.VT_PTR && tdesc.lpValue != nint.Zero)
        {
            TYPEDESC inner = Marshal.PtrToStructure<TYPEDESC>(tdesc.lpValue);
            return DecodeTypeDesc(tinfo, inner);
        }

        if (vt == VarEnum.VT_USERDEFINED)
        {
            try
            {
                int hRef = (int)tdesc.lpValue;
                tinfo.GetRefTypeInfo(hRef, out ITypeInfo refInfo);
                refInfo.GetDocumentation(-1, out string refName, out _, out _, out _);
                if (!string.IsNullOrEmpty(refName))
                {
                    return refName;
                }
            }
            catch
            {
                // Ignorar y retornar Object por defecto
            }
            return "Object";
        }

        return vt switch
        {
            VarEnum.VT_I2 => "Integer",
            VarEnum.VT_I4 => "Long",
            VarEnum.VT_R4 => "Single",
            VarEnum.VT_R8 => "Double",
            VarEnum.VT_CY => "Currency",
            VarEnum.VT_DATE => "Date",
            VarEnum.VT_BSTR => "String",
            VarEnum.VT_LPSTR => "String",
            VarEnum.VT_LPWSTR => "String",
            VarEnum.VT_DISPATCH => "Object",
            VarEnum.VT_UNKNOWN => "Object",
            VarEnum.VT_BOOL => "Boolean",
            VarEnum.VT_VARIANT => "Variant",
            VarEnum.VT_VOID => "Void",
            VarEnum.VT_EMPTY => "Void",
            VarEnum.VT_UI1 => "Byte",
            VarEnum.VT_UI2 => "Integer",
            VarEnum.VT_UI4 => "Long",
            _ => "Variant"
        };
    }

    private static OcxEnumInfo ExtractEnumInfo(ITypeInfo tinfo, TYPEATTR attr, string enumName, string enumDoc)
    {
        var enumInfo = new OcxEnumInfo
        {
            Name = enumName,
            Description = enumDoc ?? string.Empty
        };

        for (int v = 0; v < attr.cVars; v++)
        {
            nint pVarDesc = nint.Zero;
            tinfo.GetVarDesc(v, out pVarDesc);
            if (pVarDesc == nint.Zero) continue;

            try
            {
                VARDESC vd = Marshal.PtrToStructure<VARDESC>(pVarDesc);
                tinfo.GetDocumentation(vd.memid, out string varName, out string varDoc, out _, out _);

                long constValue = 0;
                if (vd.varkind == VARKIND.VAR_CONST && vd.desc.lpvarValue != nint.Zero)
                {
                    object? valObj = Marshal.GetObjectForNativeVariant(vd.desc.lpvarValue);
                    if (valObj != null)
                    {
                        try { constValue = Convert.ToInt64(valObj); } catch { }
                    }
                }

                enumInfo.Items.Add(new OcxEnumItemInfo
                {
                    Name = varName,
                    Value = constValue,
                    Description = varDoc ?? string.Empty
                });
            }
            finally
            {
                tinfo.ReleaseVarDesc(pVarDesc);
            }
        }

        return enumInfo;
    }

    private static bool IsStandardComMethod(string name)
    {
        return name.Equals("QueryInterface", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("AddRef", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("Release", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("GetTypeInfoCount", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("GetTypeInfo", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("GetIDsOfNames", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("Invoke", StringComparison.OrdinalIgnoreCase);
    }
}
