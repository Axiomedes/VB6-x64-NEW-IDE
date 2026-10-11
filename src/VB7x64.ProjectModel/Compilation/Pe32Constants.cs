using System;

namespace VB7x64.ProjectModel.Compilation;

/// <summary>
/// Constantes y firmas estándar del formato Portable Executable de 32 bits (PE32 / x86).
/// </summary>
public static class Pe32Constants
{
    // Firmas mágicas
    public const ushort DosMagic = 0x5A4D;                 // "MZ"
    public const uint PeSignature = 0x00004550;             // "PE\0\0"
    public const ushort MachineI386 = 0x014C;               // Intel 386 / x86 32-bit
    public const ushort OptionalHeaderMagicPe32 = 0x010B;   // Formato PE32 de 32 bits

    // Características del archivo ejecutable
    public const ushort CharacteristicsExecutableImage = 0x0002;
    public const ushort Characteristics32BitMachine = 0x0100;

    // Subtipos de subsistema de Windows
    public const ushort SubsystemWindowsGui = 2;            // Aplicación gráfica (Windows GUI)
    public const ushort SubsystemWindowsCui = 3;            // Aplicación de consola (Windows CUI)

    // Alineaciones estándar de Windows
    public const uint DefaultSectionAlignment = 0x1000;     // 4096 bytes en memoria
    public const uint DefaultFileAlignment = 0x0200;        // 512 bytes en disco
    public const uint DefaultImageBase = 0x00400000;        // 4 MB (Base estándar x86)

    // Flags de secciones PE
    public const uint SectionCode = 0x00000020;             // IMAGE_SCN_CNT_CODE
    public const uint SectionInitializedData = 0x00000040;  // IMAGE_SCN_CNT_INITIALIZED_DATA
    public const uint SectionExecute = 0x20000000;          // IMAGE_SCN_MEM_EXECUTE
    public const uint SectionRead = 0x40000000;             // IMAGE_SCN_MEM_READ
    public const uint SectionWrite = 0x80000000;            // IMAGE_SCN_MEM_WRITE

    // Máscaras combinadas comunes
    public const uint SectionCharacteristicsText = SectionCode | SectionExecute | SectionRead;           // 0x60000020
    public const uint SectionCharacteristicsData = SectionInitializedData | SectionRead | SectionWrite; // 0xC0000040
    public const uint SectionCharacteristicsRsrc = SectionInitializedData | SectionRead;                // 0x40000040

    // Firma mágica del runtime de Visual Basic 6
    public static readonly byte[] Vb5Magic = new byte[] { 0x56, 0x42, 0x35, 0x21 }; // "VB5!"
}
