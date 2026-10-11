using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace VB7x64.ProjectModel.Compilation;

/// <summary>
/// Emisor nativo de archivos ejecutables en formato Portable Executable de 32 bits (PE32 / x86).
/// Genera binarios estándar ejecutables en cualquier versión de Microsoft Windows de 32 y 64 bits.
/// </summary>
public class Pe32Builder
{
    private static readonly byte[] DefaultDosStub = new byte[]
    {
        0x0E, 0x1F, 0xBA, 0x0E, 0x00, 0xB4, 0x09, 0xCD, 0x21, 0xB8, 0x01, 0x4C, 0xCD, 0x21, 0x54, 0x68,
        0x69, 0x73, 0x20, 0x70, 0x72, 0x6F, 0x67, 0x72, 0x61, 0x6D, 0x20, 0x63, 0x61, 0x6E, 0x6E, 0x6F,
        0x74, 0x20, 0x62, 0x65, 0x20, 0x72, 0x75, 0x6E, 0x20, 0x69, 0x6E, 0x20, 0x44, 0x4F, 0x53, 0x20,
        0x6D, 0x6F, 0x64, 0x65, 0x2E, 0x0D, 0x0D, 0x0A, 0x24, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
    };

    public uint ImageBase { get; set; } = Pe32Constants.DefaultImageBase;
    public uint SectionAlignment { get; set; } = Pe32Constants.DefaultSectionAlignment;
    public uint FileAlignment { get; set; } = Pe32Constants.DefaultFileAlignment;
    public ushort Subsystem { get; set; } = Pe32Constants.SubsystemWindowsGui;
    public uint EntryPointRva { get; set; } = 0x1000;
    public uint ImportDirectoryRva { get; set; }
    public uint ImportDirectorySize { get; set; }
    public uint IatRva { get; set; }
    public uint IatSize { get; set; }

    private readonly List<Pe32Section> _sections = new();

    public IReadOnlyList<Pe32Section> Sections => _sections;

    public void AddSection(Pe32Section section)
    {
        if (section == null) throw new ArgumentNullException(nameof(section));
        _sections.Add(section);
    }

    /// <summary>
    /// Ensambla y genera el flujo de bytes binario completo del archivo ejecutable PE32.
    /// </summary>
    public byte[] Build()
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        // 1. Calcular offsets y RVAs de secciones
        uint currentRva = SectionAlignment; // Normalmente 0x1000 para la primera sección (.text)
        uint headersRawSize = CalculateHeadersRawSize();
        uint currentRawOffset = headersRawSize;

        foreach (var sec in _sections)
        {
            sec.VirtualAddress = currentRva;
            sec.PointerToRawData = currentRawOffset;
            sec.SizeOfRawData = AlignUp((uint)sec.DataStream.Length, FileAlignment);

            currentRva += AlignUp(Math.Max((uint)sec.DataStream.Length, 1), SectionAlignment);
            currentRawOffset += sec.SizeOfRawData;
        }

        uint sizeOfImage = currentRva;
        uint sizeOfCode = _sections.Count > 0 ? _sections[0].SizeOfRawData : 0;
        uint sizeOfInitializedData = 0;
        for (int i = 1; i < _sections.Count; i++)
        {
            sizeOfInitializedData += _sections[i].SizeOfRawData;
        }

        // 2. Escribir IMAGE_DOS_HEADER (64 bytes)
        bw.Write(Pe32Constants.DosMagic); // e_magic = "MZ" (0x5A4D)
        bw.Write((ushort)0x0090);         // e_cblp
        bw.Write((ushort)0x0003);         // e_cp
        bw.Write((ushort)0x0000);         // e_crlc
        bw.Write((ushort)0x0004);         // e_cparhdr
        bw.Write((ushort)0x0000);         // e_minalloc
        bw.Write((ushort)0xFFFF);         // e_maxalloc
        bw.Write((ushort)0x0000);         // e_ss
        bw.Write((ushort)0x00B8);         // e_sp
        bw.Write((ushort)0x0000);         // e_csum
        bw.Write((ushort)0x0000);         // e_ip
        bw.Write((ushort)0x0000);         // e_cs
        bw.Write((ushort)0x0040);         // e_lfarlc
        bw.Write((ushort)0x0000);         // e_ovno
        bw.Write(new byte[8]);            // e_res
        bw.Write((ushort)0x0000);         // e_oemid
        bw.Write((ushort)0x0000);         // e_oeminfo
        bw.Write(new byte[20]);           // e_res2
        bw.Write((uint)0x0080);           // e_lfanew = Offset a PE header (128 bytes)

        // 3. Escribir DOS Stub (64 bytes)
        bw.Write(DefaultDosStub);

        // 4. Escribir PE Signature (4 bytes en offset 0x80)
        bw.Write(Pe32Constants.PeSignature); // "PE\0\0" (0x00004550)

        // 5. Escribir IMAGE_FILE_HEADER (20 bytes)
        bw.Write(Pe32Constants.MachineI386);                                                      // Machine = i386 (0x014C)
        bw.Write((ushort)_sections.Count);                                                        // NumberOfSections
        bw.Write((uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds());                                // TimeDateStamp
        bw.Write((uint)0);                                                                        // PointerToSymbolTable
        bw.Write((uint)0);                                                                        // NumberOfSymbols
        bw.Write((ushort)0x00E0);                                                                 // SizeOfOptionalHeader = 224 bytes
        bw.Write((ushort)(Pe32Constants.CharacteristicsExecutableImage | Pe32Constants.Characteristics32BitMachine)); // 0x0102

        // 6. Escribir IMAGE_OPTIONAL_HEADER32 (224 bytes)
        bw.Write(Pe32Constants.OptionalHeaderMagicPe32); // Magic = 0x010B
        bw.Write((byte)6);                               // MajorLinkerVersion = 6 (VB6)
        bw.Write((byte)0);                               // MinorLinkerVersion = 0
        bw.Write(sizeOfCode);                            // SizeOfCode
        bw.Write(sizeOfInitializedData);                 // SizeOfInitializedData
        bw.Write((uint)0);                               // SizeOfUninitializedData
        bw.Write(EntryPointRva);                         // AddressOfEntryPoint
        bw.Write(_sections.Count > 0 ? _sections[0].VirtualAddress : SectionAlignment); // BaseOfCode
        bw.Write(_sections.Count > 1 ? _sections[1].VirtualAddress : (SectionAlignment * 2)); // BaseOfData

        bw.Write(ImageBase);                             // ImageBase (0x00400000)
        bw.Write(SectionAlignment);                      // SectionAlignment (0x1000)
        bw.Write(FileAlignment);                         // FileAlignment (0x200)
        bw.Write((ushort)4);                             // MajorOperatingSystemVersion
        bw.Write((ushort)0);                             // MinorOperatingSystemVersion
        bw.Write((ushort)1);                             // MajorImageVersion
        bw.Write((ushort)0);                             // MinorImageVersion
        bw.Write((ushort)4);                             // MajorSubsystemVersion
        bw.Write((ushort)0);                             // MinorSubsystemVersion
        bw.Write((uint)0);                               // Win32VersionValue
        bw.Write(sizeOfImage);                           // SizeOfImage
        bw.Write(headersRawSize);                        // SizeOfHeaders
        bw.Write((uint)0);                               // CheckSum
        bw.Write(Subsystem);                             // Subsystem (2 = GUI)
        bw.Write((ushort)0x0000);                         // DllCharacteristics
        bw.Write((uint)0x00100000);                      // SizeOfStackReserve (1 MB)
        bw.Write((uint)0x00001000);                      // SizeOfStackCommit (4 KB)
        bw.Write((uint)0x00100000);                      // SizeOfHeapReserve (1 MB)
        bw.Write((uint)0x00001000);                      // SizeOfHeapCommit (4 KB)
        bw.Write((uint)0);                               // LoaderFlags
        bw.Write((uint)16);                              // NumberOfRvaAndSizes

        // 16 Data Directories (8 bytes cada uno = 128 bytes)
        // [0] Export Table
        bw.Write((uint)0); bw.Write((uint)0);
        // [1] Import Table
        bw.Write(ImportDirectoryRva); bw.Write(ImportDirectorySize);
        // [2] Resource Table
        bw.Write((uint)0); bw.Write((uint)0);
        // [3] Exception Table
        bw.Write((uint)0); bw.Write((uint)0);
        // [4] Certificate Table
        bw.Write((uint)0); bw.Write((uint)0);
        // [5] Base Relocation Table
        bw.Write((uint)0); bw.Write((uint)0);
        // [6] Debug
        bw.Write((uint)0); bw.Write((uint)0);
        // [7] Architecture
        bw.Write((uint)0); bw.Write((uint)0);
        // [8] Global Ptr
        bw.Write((uint)0); bw.Write((uint)0);
        // [9] TLS Table
        bw.Write((uint)0); bw.Write((uint)0);
        // [10] Load Config Table
        bw.Write((uint)0); bw.Write((uint)0);
        // [11] Bound Import
        bw.Write((uint)0); bw.Write((uint)0);
        // [12] IAT (Import Address Table)
        bw.Write(IatRva); bw.Write(IatSize);
        // [13] Delay Import
        bw.Write((uint)0); bw.Write((uint)0);
        // [14] CLR Runtime Header
        bw.Write((uint)0); bw.Write((uint)0);
        // [15] Reserved
        bw.Write((uint)0); bw.Write((uint)0);

        // 7. Escribir IMAGE_SECTION_HEADERs (40 bytes cada una)
        foreach (var sec in _sections)
        {
            byte[] nameBytes = new byte[8];
            Encoding.ASCII.GetBytes(sec.Name, 0, Math.Min(sec.Name.Length, 8), nameBytes, 0);
            bw.Write(nameBytes);                         // Name (8 bytes)
            bw.Write(sec.VirtualSize);                   // VirtualSize
            bw.Write(sec.VirtualAddress);                // VirtualAddress
            bw.Write(sec.SizeOfRawData);                 // SizeOfRawData
            bw.Write(sec.PointerToRawData);              // PointerToRawData
            bw.Write((uint)0);                           // PointerToRelocations
            bw.Write((uint)0);                           // PointerToLinenumbers
            bw.Write((ushort)0);                         // NumberOfRelocations
            bw.Write((ushort)0);                         // NumberOfLinenumbers
            bw.Write(sec.Characteristics);               // Characteristics
        }

        // 8. Rellenar hasta el tamaño de cabeceras alineado (headersRawSize)
        long currentHeaderPos = ms.Position;
        if (currentHeaderPos < headersRawSize)
        {
            bw.Write(new byte[headersRawSize - currentHeaderPos]);
        }

        // 9. Escribir el contenido binario de cada sección alineado a FileAlignment
        foreach (var sec in _sections)
        {
            byte[] secData = sec.ToArray();
            bw.Write(secData);

            int padding = (int)(sec.SizeOfRawData - secData.Length);
            if (padding > 0)
            {
                bw.Write(new byte[padding]);
            }
        }

        bw.Flush();
        return ms.ToArray();
    }

    private uint CalculateHeadersRawSize()
    {
        // DOS Header (64) + DOS Stub (64) + PE Sig (4) + COFF Header (20) + Optional Header (224) + Section Headers (N * 40)
        uint size = 64 + 64 + 4 + 20 + 224 + (uint)(_sections.Count * 40);
        return AlignUp(size, FileAlignment);
    }

    private static uint AlignUp(uint value, uint alignment)
    {
        if (alignment == 0) return value;
        uint remainder = value % alignment;
        return remainder == 0 ? value : value + (alignment - remainder);
    }
}
