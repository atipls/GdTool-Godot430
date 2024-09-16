using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;

namespace GdTool;

static class AssetDecompressor {
    private struct GodotStreamableTextureFormat(BinaryReader binaryReader) {
        public const int MAGIC = 0x32545347;
        public uint Version = binaryReader.ReadUInt32();
        public uint Width = binaryReader.ReadUInt32();
        public uint Height = binaryReader.ReadUInt32();
        public uint DataFormat = binaryReader.ReadUInt32();
        public uint MipMapLimit = binaryReader.ReadUInt32();
        public uint TextureFormat = binaryReader.ReadUInt32();
        public ushort TextureWidth = binaryReader.ReadUInt16();
        public ushort TextureHeight = binaryReader.ReadUInt16();
        public uint MipMapCount = binaryReader.ReadUInt32();
        public uint ImageFormat = binaryReader.ReadUInt32();
    }

    [Flags]
    private enum GodotResourceFormatFlags {
        FORMAT_FLAG_NAMED_SCENE_IDS = 1,
        FORMAT_FLAG_UIDS = 2,
        FORMAT_FLAG_REAL_T_IS_DOUBLE = 4,
        FORMAT_FLAG_HAS_SCRIPT_CLASS = 8,
    };

    enum WavFormat {
        FORMAT_8_BITS,
        FORMAT_16_BITS,
        FORMAT_IMA_ADPCM
    };


    enum AssetVariantTypde {
        //numbering must be different from variant, in case new variant types are added (variant must be always contiguous for jumptable optimization)
        NIL = 1,
        BOOL = 2,
        INT = 3,
        FLOAT = 4,
        STRING = 5,
        VECTOR2 = 10,
        RECT2 = 11,
        VECTOR3 = 12,
        PLANE = 13,
        QUATERNION = 14,
        AABB = 15,
        BASIS = 16,
        TRANSFORM3D = 17,
        TRANSFORM2D = 18,
        COLOR = 20,
        NODE_PATH = 22,
        RID = 23,
        OBJECT = 24,
        INPUT_EVENT = 25,
        DICTIONARY = 26,
        ARRAY = 30,
        PACKED_BYTE_ARRAY = 31,
        PACKED_INT32_ARRAY = 32,
        PACKED_FLOAT32_ARRAY = 33,
        PACKED_STRING_ARRAY = 34,
        PACKED_VECTOR3_ARRAY = 35,
        PACKED_COLOR_ARRAY = 36,
        PACKED_VECTOR2_ARRAY = 37,
        INT64 = 40,
        DOUBLE = 41,
        CALLABLE = 42,
        SIGNAL = 43,
        STRING_NAME = 44,
        VECTOR2I = 45,
        RECT2I = 46,
        VECTOR3I = 47,
        PACKED_INT64_ARRAY = 48,
        PACKED_FLOAT64_ARRAY = 49,
        VECTOR4 = 50,
        VECTOR4I = 51,
        PROJECTION = 52,
        PACKED_VECTOR4_ARRAY = 53,
        OBJECT_EMPTY = 0,
        OBJECT_EXTERNAL_RESOURCE = 1,
        OBJECT_INTERNAL_RESOURCE = 2,
        OBJECT_EXTERNAL_RESOURCE_INDEX = 3,
        // Version 2: Added 64-bit support for float and int.
        // Version 3: Changed NodePath encoding.
        // Version 4: New string ID for ext/subresources, breaks forward compat.
        // Version 5: Ability to store script class in the header.
        // Version 6: Added PackedVector4Array Variant type.
        FORMAT_VERSION = 6,
        FORMAT_VERSION_CAN_RENAME_DEPS = 1,
        FORMAT_VERSION_NO_NODEPATH_PROPERTY = 3,
    };


    public static byte[] DecompressTexture(byte[] data) {
        using var memoryStream = new MemoryStream(data);
        using var binaryReader = new BinaryReader(memoryStream);

        var magic = binaryReader.ReadUInt32();

        if (magic == GodotStreamableTextureFormat.MAGIC) {
            var gst2 = new GodotStreamableTextureFormat(binaryReader);
        } else {
            throw new Exception("Invalid texture format");
        }

        binaryReader.ReadBytes(0x10);

        return data[(int)binaryReader.BaseStream.Position..];
    }

    public static byte[] DecompressResource(byte[] data) {
        using var memoryStream = new MemoryStream(data);
        using var binaryReader = new BinaryReader(memoryStream);

        var magic = binaryReader.ReadUInt32();
        const uint MAGIC_RSCC = 0x43435352; // Compressed
        const uint MAGIC_RSRC = 0x43525352; // Uncompressed

        if (magic != MAGIC_RSCC && magic != MAGIC_RSRC) {
            throw new Exception("Invalid resource format");
        }

        if (magic == MAGIC_RSCC) {
            Console.WriteLine("Not yet");
            return data;
        }

        var bigEndian = binaryReader.ReadInt32();
        if (bigEndian > 0) Console.WriteLine("Big endian resources are currently not supported. Extracted file might not be readable.");

        binaryReader.BaseStream.Seek(4, SeekOrigin.Current);
        var versionMajor = binaryReader.ReadInt32();
        var versionMinor = binaryReader.ReadInt32();
        var versionFormat = binaryReader.ReadInt32();

        var resourceType = binaryReader.Read32BitPrefixedString(false);
        if (resourceType == "AudioStreamOggVorbis") {
            Console.WriteLine("AudioStreamOggVorbis resource, skipping");
            return data;
        }

        var importMdOffset = binaryReader.ReadUInt64(); // importmd_ofs
        var flags = (GodotResourceFormatFlags)binaryReader.ReadInt32();
        var uidData = binaryReader.ReadUInt64();

        Console.WriteLine($"{resourceType} resource, version {versionMajor}.{versionMinor}.{versionFormat} (flags: 0x{flags:X})");

        binaryReader.BaseStream.Seek(11 * 4, SeekOrigin.Current); // reserved fields

        var stringTableSize = binaryReader.ReadInt32();
        var stringTable = new string[stringTableSize];
        for (var i = 0; i < stringTableSize; i++) {
            stringTable[i] = binaryReader.Read32BitPrefixedString(false);
        }

        var externalResourceCount = binaryReader.ReadInt32();
        for (var i = 0; i < externalResourceCount; i++) {
            var type = binaryReader.Read32BitPrefixedString(false);
            var path = binaryReader.Read32BitPrefixedString(false);
            Console.WriteLine($"External resource: {type} {path}");
        }

        var internalResourceCount = binaryReader.ReadInt32();
        var internalResourceOffsets = new long[internalResourceCount];
        for (var i = 0; i < internalResourceCount; i++) {
            var path = binaryReader.Read32BitPrefixedString(false);
            internalResourceOffsets[i] = binaryReader.ReadInt64();
            Console.WriteLine($"[{i}] Internal resource: {path} @ {internalResourceOffsets[i]}");
        }

        binaryReader.BaseStream.Seek(internalResourceOffsets[0], SeekOrigin.Begin);
        var name = binaryReader.Read32BitPrefixedString(false);
        var propertyCount = binaryReader.ReadInt32();
        var properties = new Dictionary<string, Variant>();
        Console.WriteLine($"Resource: {name} ({propertyCount} properties)");
        for (int i = 0; i < propertyCount; i++) {
            var nameIndex = binaryReader.ReadInt32();
            var value = ReadVariant(binaryReader);
            properties[stringTable[nameIndex]] = value;
        }

        if (!properties.TryGetValue("data", out var rawData)) {
            Console.WriteLine("Resource does not contain data property");
            return data;
        }

        properties.TryGetValue("stereo", out var stereo);

        var writerMemoryStream = new MemoryStream(44);
        var binaryWriter = new BinaryWriter(writerMemoryStream);

        var waveformData = rawData.Value as byte[];

        var subChunk2Size = (int)waveformData.Length * 8;
        var channels = (stereo?.Value ?? false) ? 2 : 1;
        var formatCode = (WavFormat)properties["format"].Value;
        var sampleRate = properties.TryGetValue("mix_rate", out var sampleRateRaw) ? (int)sampleRateRaw.Value : 44100;
        var bytesPerSample = formatCode == WavFormat.FORMAT_8_BITS ? 1 : formatCode == WavFormat.FORMAT_16_BITS ? 2 : 4;


        binaryWriter.Write(Encoding.ASCII.GetBytes("RIFF"));
        binaryWriter.Write(BitConverter.GetBytes(subChunk2Size + 36));
        binaryWriter.Write(Encoding.ASCII.GetBytes("WAVE"));
        binaryWriter.Write(Encoding.ASCII.GetBytes("fmt "));
        binaryWriter.Write(BitConverter.GetBytes(16));
        binaryWriter.Write(BitConverter.GetBytes((short)formatCode));
        binaryWriter.Write(BitConverter.GetBytes((short)channels));
        binaryWriter.Write(BitConverter.GetBytes(sampleRate));
        binaryWriter.Write(BitConverter.GetBytes(sampleRate * channels * bytesPerSample));
        binaryWriter.Write(BitConverter.GetBytes((short)(channels * bytesPerSample)));
        binaryWriter.Write(BitConverter.GetBytes((short)(bytesPerSample * 8)));
        binaryWriter.Write(Encoding.ASCII.GetBytes("data"));
        binaryWriter.Write(BitConverter.GetBytes(subChunk2Size));
        binaryWriter.Write(waveformData);

        return writerMemoryStream.ToArray();
    }

    public static Variant ReadVariant(BinaryReader reader) {
        var variantHeader = reader.ReadInt32();
        var variantType = (AssetVariantTypde)(variantHeader & 0xFF);
        switch (variantType) {
            case AssetVariantTypde.NIL: return Variant.OfNil;
            case AssetVariantTypde.BOOL: return Variant.OfBool(reader.ReadInt32() != 0);
            case AssetVariantTypde.INT: return Variant.OfInt(reader.ReadInt32());
            case AssetVariantTypde.INT64: return Variant.OfInt64(reader.ReadInt64());
            case AssetVariantTypde.FLOAT: return Variant.OfFloat(reader.ReadSingle());
            case AssetVariantTypde.DOUBLE: return Variant.OfFloat64(reader.ReadDouble());
            case AssetVariantTypde.STRING: return Variant.OfString(reader.Read32BitPrefixedString(true));
            case AssetVariantTypde.VECTOR2: {
                    var x = reader.ReadSingle();
                    var y = reader.ReadSingle();
                    return Variant.OfVector2(new Vector2() { X = x, Y = y });
                }
            case AssetVariantTypde.RECT2I: {
                    var x = reader.ReadInt32();
                    var y = reader.ReadInt32();
                    var width = reader.ReadInt32();
                    var height = reader.ReadInt32();
                    return Variant.OfRect2i(new { Position = new { X = x, Y = y }, Size = new { X = width, Y = height } });
                }
            case AssetVariantTypde.VECTOR4I: {
                    var x = reader.ReadInt32();
                    var y = reader.ReadInt32();
                    var z = reader.ReadInt32();
                    var w = reader.ReadInt32();
                    return Variant.OfVector4i(new Vector4() { X = x, Y = y, Z = z, W = w });
                }
            case AssetVariantTypde.PACKED_STRING_ARRAY: {
                    var arrayLength = reader.ReadInt32();
                    var array = new string[arrayLength];
                    for (var j = 0; j < arrayLength; j++)
                        array[j] = reader.Read32BitPrefixedString(true);
                    return Variant.OfPackedStringArray(array);
                }
            case AssetVariantTypde.COLOR: {
                    var r = reader.ReadSingle();
                    var g = reader.ReadSingle();
                    var b = reader.ReadSingle();
                    var a = reader.ReadSingle();
                    return Variant.OfColor(new Color() { R = r, G = g, B = b, A = a });
                }
            case AssetVariantTypde.RID: return Variant.OfRID(reader.ReadUInt64());
            case AssetVariantTypde.OBJECT: {
                    var className = reader.Read32BitPrefixedString(true);
                    var numProperties = reader.ReadInt32();
                    var properties = new Dictionary<string, Variant>(numProperties);
                    for (var j = 0; j < numProperties; j++) {
                        var key = reader.Read32BitPrefixedString(true);
                        var value = ReadVariant(reader);
                        properties[key] = value;
                    }

                    return Variant.OfObject(new VariantObject(className, properties));
                }
            case AssetVariantTypde.DICTIONARY: {
                    var numEntries = reader.ReadInt32();
                    var dictionary = new Dictionary<Variant, Variant>(numEntries);
                    for (var j = 0; j < numEntries; j++) {
                        var key = ReadVariant(reader);
                        var value = ReadVariant(reader);
                        dictionary[key] = value;
                    }

                    return Variant.OfDictionary(dictionary);
                }
            case AssetVariantTypde.ARRAY: {
                    var numEntries = reader.ReadInt32();
                    var array = new Variant[numEntries];
                    for (var j = 0; j < numEntries; j++) {
                        array[j] = ReadVariant(reader);
                    }

                    return Variant.OfArray(array);
                }
            case AssetVariantTypde.PACKED_INT32_ARRAY: {
                    var arrayLength = reader.ReadInt32();
                    var array = reader.ReadArray<int>(arrayLength / 4);

                    return Variant.OfPackedInt32Array(array);
                }
            case AssetVariantTypde.PACKED_INT64_ARRAY: {
                    var arrayLength = reader.ReadInt32();
                    var array = reader.ReadArray<long>(arrayLength / 8);

                    return Variant.OfPackedInt64Array(array);
                }
            case AssetVariantTypde.PACKED_BYTE_ARRAY: {
                    var arrayLength = reader.ReadInt32();
                    var array = reader.ReadBytes(arrayLength);
                    if (arrayLength % 4 != 0) reader.BaseStream.Seek(4 - (arrayLength % 4), SeekOrigin.Current);
                    
                    return Variant.OfPackedByteArray(array);
                }
            default:
                throw new Exception("Unsupported variant type " + variantType);
        }
    }
}