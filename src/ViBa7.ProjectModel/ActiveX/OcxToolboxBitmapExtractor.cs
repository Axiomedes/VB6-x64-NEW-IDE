using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace ViBa7.ProjectModel.ActiveX;

/// <summary>
/// Extrae el icono real de Caja de Herramientas (ToolboxBitmap) de un control ActiveX compilado (OCX)
/// sin instanciarlo ni ejecutar su código:
/// 1. Lee <c>HKCR\CLSID\{clsid}\ToolboxBitmap32</c> (vista de 64 y de 32 bits) con formato "ruta, idRecurso".
/// 2. Carga el binario como archivo de datos (LOAD_LIBRARY_AS_DATAFILE), válido también para OCX de 32 bits desde el IDE x64.
/// 3. Localiza el recurso RT_BITMAP y lo devuelve como archivo .bmp completo (agregando BITMAPFILEHEADER).
/// </summary>
public static class OcxToolboxBitmapExtractor
{
    private const uint LOAD_LIBRARY_AS_DATAFILE = 0x00000002;
    private const uint LOAD_LIBRARY_AS_IMAGE_RESOURCE = 0x00000020;
    private static readonly IntPtr RT_BITMAP = new(2);

    private static readonly ConcurrentDictionary<string, byte[]?> _cache = new(StringComparer.OrdinalIgnoreCase);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, uint dwFlags);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FreeLibrary(IntPtr hModule);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr FindResource(IntPtr hModule, IntPtr lpName, IntPtr lpType);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "FindResourceW")]
    private static extern IntPtr FindResourceByName(IntPtr hModule, string lpName, IntPtr lpType);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LoadResource(IntPtr hModule, IntPtr hResInfo);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LockResource(IntPtr hResData);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint SizeofResource(IntPtr hModule, IntPtr hResInfo);

    /// <summary>
    /// Obtiene el ToolboxBitmap (bytes de un .bmp) de una CoClass registrada.
    /// </summary>
    /// <param name="clsid">CLSID de la CoClass del control (con o sin llaves).</param>
    /// <param name="fallbackFilePath">Ruta del OCX a usar si el registro no indica una ruta válida.</param>
    public static byte[]? GetToolboxBitmap(string? clsid, string? fallbackFilePath = null)
    {
        if (!OperatingSystem.IsWindows()) return null;
        if (string.IsNullOrWhiteSpace(clsid) && string.IsNullOrWhiteSpace(fallbackFilePath)) return null;

        string cacheKey = $"{clsid}|{fallbackFilePath}";
        return _cache.GetOrAdd(cacheKey, _ => ResolveToolboxBitmap(clsid, fallbackFilePath));
    }

    private static byte[]? ResolveToolboxBitmap(string? clsid, string? fallbackFilePath)
    {
        try
        {
            string? path = null;
            string? resourceId = null;

            if (!string.IsNullOrWhiteSpace(clsid))
            {
                string? regValue = ReadToolboxBitmapRegistryValue(clsid);
                if (!string.IsNullOrWhiteSpace(regValue))
                {
                    ParseToolboxBitmapValue(regValue, out path, out resourceId);
                }
            }

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                path = fallbackFilePath;
            }

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path) || string.IsNullOrWhiteSpace(resourceId))
            {
                return null;
            }

            return LoadBitmapResource(path, resourceId);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Separa un valor de registro "C:\ruta\control.ocx, 1" en ruta e identificador de recurso.
    /// </summary>
    public static void ParseToolboxBitmapValue(string value, out string? path, out string? resourceId)
    {
        path = null;
        resourceId = null;
        if (string.IsNullOrWhiteSpace(value)) return;

        string clean = Environment.ExpandEnvironmentVariables(value.Trim());
        int comma = clean.LastIndexOf(',');
        if (comma > 0)
        {
            path = clean[..comma].Trim().Trim('"');
            resourceId = clean[(comma + 1)..].Trim();
        }
        else
        {
            path = clean.Trim('"');
        }
    }

    private static string? ReadToolboxBitmapRegistryValue(string clsid)
    {
        if (!OperatingSystem.IsWindows()) return null;

        string clean = clsid.Trim();
        if (!clean.StartsWith('{')) clean = "{" + clean + "}";

        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using var root = RegistryKey.OpenBaseKey(RegistryHive.ClassesRoot, view);
                foreach (var subName in new[] { "ToolboxBitmap32", "ToolboxBitmap" })
                {
                    using var key = root.OpenSubKey($@"CLSID\{clean}\{subName}");
                    if (key?.GetValue(null) is string s && !string.IsNullOrWhiteSpace(s))
                    {
                        return s;
                    }
                }
            }
            catch
            {
                // Continuar con la siguiente vista del registro
            }
        }

        return null;
    }

    /// <summary>
    /// Carga un recurso RT_BITMAP de un binario PE (OCX/DLL) como archivo de datos y lo devuelve como .bmp completo.
    /// </summary>
    public static byte[]? LoadBitmapResource(string peFilePath, string resourceId)
    {
        if (!OperatingSystem.IsWindows() || !File.Exists(peFilePath)) return null;

        IntPtr hModule = LoadLibraryEx(peFilePath, IntPtr.Zero, LOAD_LIBRARY_AS_DATAFILE | LOAD_LIBRARY_AS_IMAGE_RESOURCE);
        if (hModule == IntPtr.Zero)
        {
            hModule = LoadLibraryEx(peFilePath, IntPtr.Zero, LOAD_LIBRARY_AS_DATAFILE);
        }
        if (hModule == IntPtr.Zero) return null;

        try
        {
            IntPtr hResInfo;
            if (int.TryParse(resourceId, NumberStyles.Integer, CultureInfo.InvariantCulture, out int numericId) && numericId > 0 && numericId <= 0xFFFF)
            {
                hResInfo = FindResource(hModule, new IntPtr(numericId), RT_BITMAP);
            }
            else
            {
                hResInfo = FindResourceByName(hModule, resourceId.TrimStart('#'), RT_BITMAP);
            }
            if (hResInfo == IntPtr.Zero) return null;

            uint size = SizeofResource(hModule, hResInfo);
            IntPtr hResData = LoadResource(hModule, hResInfo);
            IntPtr pData = hResData != IntPtr.Zero ? LockResource(hResData) : IntPtr.Zero;
            if (pData == IntPtr.Zero || size < 40) return null;

            byte[] dib = new byte[size];
            Marshal.Copy(pData, dib, 0, (int)size);
            return WrapDibAsBmp(dib);
        }
        finally
        {
            FreeLibrary(hModule);
        }
    }

    /// <summary>
    /// Antepone un BITMAPFILEHEADER (14 bytes) a un DIB empaquetado (BITMAPINFOHEADER + paleta + píxeles).
    /// </summary>
    public static byte[]? WrapDibAsBmp(byte[] dib)
    {
        if (dib == null || dib.Length < 12) return null;

        int headerSize = BitConverter.ToInt32(dib, 0);
        int paletteBytes = 0;

        if (headerSize == 12)
        {
            // BITMAPCOREHEADER (OS/2): entradas RGBTRIPLE de 3 bytes
            int bitCountCore = BitConverter.ToUInt16(dib, 10);
            if (bitCountCore <= 8) paletteBytes = (1 << bitCountCore) * 3;
        }
        else if (headerSize >= 40 && dib.Length >= 40)
        {
            int bitCount = BitConverter.ToUInt16(dib, 14);
            int compression = BitConverter.ToInt32(dib, 16);
            int clrUsed = BitConverter.ToInt32(dib, 32);

            if (bitCount <= 8)
            {
                paletteBytes = (clrUsed > 0 ? clrUsed : (1 << bitCount)) * 4;
            }
            else if (clrUsed > 0)
            {
                paletteBytes = clrUsed * 4;
            }

            // BI_BITFIELDS con BITMAPINFOHEADER clásico: 3 máscaras DWORD tras la cabecera
            if (compression == 3 && headerSize == 40)
            {
                paletteBytes += 12;
            }
        }
        else
        {
            return null;
        }

        int pixelOffset = 14 + headerSize + paletteBytes;
        int fileSize = 14 + dib.Length;

        byte[] bmp = new byte[fileSize];
        bmp[0] = (byte)'B';
        bmp[1] = (byte)'M';
        BitConverter.GetBytes(fileSize).CopyTo(bmp, 2);
        BitConverter.GetBytes(pixelOffset).CopyTo(bmp, 10);
        Buffer.BlockCopy(dib, 0, bmp, 14, dib.Length);
        return bmp;
    }
}
