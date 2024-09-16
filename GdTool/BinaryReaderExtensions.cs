using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace GdTool;

public static class BinaryReaderExtensions {
    public static string Read32BitPrefixedString(this BinaryReader binaryReader, bool hasPadding) {
        var stringLength = binaryReader.ReadUInt32();
        var stringData = Encoding.UTF8.GetString(binaryReader.ReadBytes((int)stringLength));
        if (hasPadding && stringLength % 4 != 0)
            binaryReader.BaseStream.Seek(4 - (int)stringLength % 4, SeekOrigin.Current);
        return stringData.TrimEnd('\0');
    }

    public static T[] ReadArray<T>(this BinaryReader binaryReader, int count) where T: struct {
        var size = count * Unsafe.SizeOf<T>();
        var memory = GC.AllocateUninitializedArray<byte>(size);
        binaryReader.Read(memory);
        return MemoryMarshal.Cast<byte, T>(memory).ToArray();
    }

    public static byte[] ToByteArray<T>(this T[] array) where T: struct {
        var size = array.Length * Unsafe.SizeOf<T>();
        var memory = MemoryMarshal.Cast<T, byte>(array);
        var byteArray = new byte[size];
        memory.CopyTo(byteArray);
        return byteArray;
    }
}