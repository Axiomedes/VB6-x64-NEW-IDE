using System;
using System.IO;

namespace ViBa7.ProjectModel.Compilation;

/// <summary>
/// Representa una sección de un archivo ejecutable PE32 (.text, .data, etc.).
/// </summary>
public class Pe32Section
{
    public string Name { get; set; }
    public uint Characteristics { get; set; }
    public MemoryStream DataStream { get; } = new();

    public uint VirtualAddress { get; set; }
    public uint VirtualSize => (uint)DataStream.Length;
    public uint PointerToRawData { get; set; }
    public uint SizeOfRawData { get; set; }

    public Pe32Section(string name, uint characteristics)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Characteristics = characteristics;
    }

    public byte[] ToArray() => DataStream.ToArray();

    public void WriteBytes(byte[] bytes)
    {
        DataStream.Write(bytes, 0, bytes.Length);
    }

    public void WriteByte(byte b)
    {
        DataStream.WriteByte(b);
    }

    public void WriteUInt16(ushort value)
    {
        DataStream.WriteByte((byte)(value & 0xFF));
        DataStream.WriteByte((byte)((value >> 8) & 0xFF));
    }

    public void WriteUInt32(uint value)
    {
        DataStream.WriteByte((byte)(value & 0xFF));
        DataStream.WriteByte((byte)((value >> 8) & 0xFF));
        DataStream.WriteByte((byte)((value >> 16) & 0xFF));
        DataStream.WriteByte((byte)((value >> 24) & 0xFF));
    }

    public void WriteZeroes(int count)
    {
        if (count <= 0) return;
        byte[] zeroes = new byte[count];
        DataStream.Write(zeroes, 0, count);
    }

    public void PadToAlignment(uint alignment)
    {
        long current = DataStream.Length;
        long remainder = current % alignment;
        if (remainder != 0)
        {
            int toAdd = (int)(alignment - remainder);
            WriteZeroes(toAdd);
        }
    }
}
