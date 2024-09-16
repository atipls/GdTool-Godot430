using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;

namespace GdTool;

class ConfigFile {
    // Byte 0: `Variant::Type`, byte 1: unused, bytes 2 and 3: additional data.
    const int HEADER_TYPE_MASK = 0xFF;

    // For `Variant::INT`, `Variant::FLOAT` and other math types.
    const int HEADER_DATA_FLAG_64 = (1 << 16);

    // For `Variant::OBJECT`.
    const int HEADER_DATA_FLAG_OBJECT_AS_ID = (1 << 16);

    // For `Variant::ARRAY`.
    // Occupies bits 16 and 17.
    const int HEADER_DATA_FIELD_TYPED_ARRAY_MASK = (0b11 << 16);
    const int HEADER_DATA_FIELD_TYPED_ARRAY_NONE = (0b00 << 16);
    const int HEADER_DATA_FIELD_TYPED_ARRAY_BUILTIN = (0b01 << 16);
    const int HEADER_DATA_FIELD_TYPED_ARRAY_CLASS_NAME = (0b10 << 16);
    const int HEADER_DATA_FIELD_TYPED_ARRAY_SCRIPT = (0b11 << 16);


    private readonly Dictionary<string, Dictionary<string, string>> mSections = [];

    public static ConfigFile Load(byte[] bytes) {
        using var reader = new StreamReader(new MemoryStream(bytes));
        var configFile = new ConfigFile();
        string currentSection = null;

        string currentBuffer = "";
        while (!reader.EndOfStream) {
            var character = (char)reader.Read();
            if (character == '[') {
                currentSection = reader.ReadLine().Trim();
                currentSection = currentSection[..^1];
                currentBuffer = "";
                continue;
            }

            if (char.IsWhiteSpace(character))
                continue;

            if (character == '=') {
                var key = currentBuffer.Trim();
                currentBuffer = "";

                while (!reader.EndOfStream) {
                    character = (char)reader.Read();
                    if (character == '=')
                        continue;

                    if (character == '\n')
                        break;

                    if (character == '{') {
                        currentBuffer += character;
                        while (!reader.EndOfStream) {
                            character = (char)reader.Read();
                            currentBuffer += character;

                            if (character == '}')
                                break;
                        }
                        break;
                    }

                    currentBuffer += character;
                }

                var value = currentBuffer;
                currentBuffer = "";

                if (!configFile.mSections.ContainsKey(currentSection))
                    configFile.mSections[currentSection] = [];

                configFile.mSections[currentSection][key] = value;

                continue;
            }

            currentBuffer += character;
        }

        return configFile;
    }

    public static Variant ReadVariant(BinaryReader reader) {
        var variantHeader = reader.ReadInt32();
        var variantType = (VariantType)(variantHeader & HEADER_TYPE_MASK);
        switch (variantType) {
            case VariantType.NIL: return Variant.OfNil;
            case VariantType.BOOL: return Variant.OfBool(reader.ReadInt32() != 0);
            case VariantType.INT: return (variantHeader & HEADER_DATA_FLAG_64) != 0 ? Variant.OfInt64(reader.ReadInt64()) : Variant.OfInt(reader.ReadInt32());
            case VariantType.FLOAT: return (variantHeader & HEADER_DATA_FLAG_64) != 0 ? Variant.OfFloat64(reader.ReadDouble()) : Variant.OfFloat(reader.ReadSingle());
            case VariantType.STRING: return Variant.OfString(reader.Read32BitPrefixedString(true));
            case VariantType.VECTOR2: {
                    var x = (variantHeader & HEADER_DATA_FLAG_64) != 0 ? reader.ReadDouble() : reader.ReadSingle();
                    var y = (variantHeader & HEADER_DATA_FLAG_64) != 0 ? reader.ReadDouble() : reader.ReadSingle();
                    return Variant.OfVector2(new Vector2() { X = (float)x, Y = (float)y });
                }
                case VariantType.RECT2I: {
                    var x = reader.ReadInt32();
                    var y = reader.ReadInt32();
                    var width = reader.ReadInt32();
                    var height = reader.ReadInt32();
                    return Variant.OfRect2i(new { Position = new { X = x, Y = y }, Size = new { X = width, Y = height } });
                }
            case VariantType.VECTOR4I: {
                    var x = reader.ReadInt32();
                    var y = reader.ReadInt32();
                    var z = reader.ReadInt32();
                    var w = reader.ReadInt32();
                    return Variant.OfVector4i(new Vector4() { X = x, Y = y, Z = z, W = w });
                }
            case VariantType.PACKED_STRING_ARRAY: {
                    var arrayLength = reader.ReadInt32();
                    var array = new string[arrayLength];
                    for (var j = 0; j < arrayLength; j++)
                        array[j] = reader.Read32BitPrefixedString(true);
                    return Variant.OfPackedStringArray(array);
                }
            case VariantType.COLOR: {
                    var r = reader.ReadSingle();
                    var g = reader.ReadSingle();
                    var b = reader.ReadSingle();
                    var a = reader.ReadSingle();
                    return Variant.OfColor(new Color() { R = r, G = g, B = b, A = a });
                }
            case VariantType.RID: return Variant.OfRID(reader.ReadUInt64());
            case VariantType.OBJECT: {
                    var objectAsId = (variantHeader & HEADER_DATA_FLAG_OBJECT_AS_ID) != 0;
                    if (objectAsId) {
                        var id = reader.ReadInt64();
                        return Variant.OfObject(id);
                    } else {
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
                }
            case VariantType.DICTIONARY: {
                    var numEntries = reader.ReadInt32();
                    var dictionary = new Dictionary<Variant, Variant>(numEntries);
                    for (var j = 0; j < numEntries; j++) {
                        var key = ReadVariant(reader);
                        var value = ReadVariant(reader);
                        dictionary[key] = value;
                    }

                    return Variant.OfDictionary(dictionary);
                }
            case VariantType.ARRAY: {
                    switch (variantHeader & HEADER_DATA_FIELD_TYPED_ARRAY_MASK) {
                        case HEADER_DATA_FIELD_TYPED_ARRAY_NONE:
                            break;
                        default:
                            throw new Exception("Unsupported typed array type " + (variantHeader & HEADER_DATA_FIELD_TYPED_ARRAY_MASK));
                    }

                    var numEntries = reader.ReadInt32();
                    var array = new Variant[numEntries];
                    for (var j = 0; j < numEntries; j++) {
                        array[j] = ReadVariant(reader);
                    }

                    return Variant.OfArray(array);
                }
            case VariantType.PACKED_INT32_ARRAY: {
                    var arrayLength = reader.ReadInt32();
                    var array = reader.ReadArray<int>(arrayLength / 4);

                    return Variant.OfPackedInt32Array(array);
                }
            case VariantType.PACKED_INT64_ARRAY: {
                    var arrayLength = reader.ReadInt32();
                    var array = reader.ReadArray<long>(arrayLength / 8);

                    return Variant.OfPackedInt64Array(array);
                }
            default:
                throw new Exception("Unsupported variant type " + variantType);
        }
    }

    private static (string, string, Variant) ReadSingleValue(BinaryReader reader, ConfigFile configFile) {

        var keyLength = reader.ReadInt32();
        var key = Encoding.UTF8.GetString(reader.ReadBytes(keyLength));

        var section = key.Split('/')[0];
        key = key[(section.Length + 1)..];

        var variantLength = reader.ReadInt32();
        var readStart = reader.BaseStream.Position;
        try {
            var value = ReadVariant(reader);
            return (section, key, value);
        } catch (Exception e) {
            Console.WriteLine(e);
            var readSoFar = reader.BaseStream.Position;
            var remaining = readStart + variantLength - readSoFar;
            reader.BaseStream.Seek(remaining, SeekOrigin.Current);
            return (section, key, Variant.OfNil);
        }

    }

    public static ConfigFile LoadBinary(byte[] data) {
        using var reader = new BinaryReader(new MemoryStream(data));
        var configFile = new ConfigFile();

        var magic = reader.ReadUInt32();
        if (magic != 0x47464345)
            throw new Exception("Invalid config file");

        var numConfigEntries = reader.ReadInt32();

        for (var i = 0; i < numConfigEntries; i++) {
            var (section, key, value) = ReadSingleValue(reader, configFile);
            configFile.AddKey(section, key, value.ToString());
        }


        return configFile;
    }

    public void AddSection(string section) => mSections[section] = [];

    public void AddKey(string section, string key, string value) {
        if (!mSections.ContainsKey(section))
            mSections[section] = [];

        mSections[section][key] = value;
    }

    public bool TryGet(string section, string key, out string value) => mSections[section].TryGetValue(key, out value);

    public string Serialize() {
        var sb = new StringBuilder();
        foreach (var (section, keys) in mSections) {
            sb.AppendLine($"[{section}]\n");
            foreach (var (key, value) in keys) {
                sb.AppendLine($"{key} = {value}");
            }

            sb.AppendLine();
            sb.AppendLine();
        }

        return sb.ToString();
    }
}