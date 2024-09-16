using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ZstdSharp;

namespace GdTool
{
    public class GdScriptDecompiler
    {
        private static Decompressor mZstdDecompressor = new Decompressor();
        private static string DecompileV2(BytecodeProvider provider, BinaryReader compressedBinaryReader, out string[] identifiers, out IGdStructure[] constants, out List<GdcToken> tokens, out Dictionary<uint, uint> tokenLineMap)
        {
            var version = compressedBinaryReader.ReadUInt32();
            if (version > 100)
            {
                throw new Exception("Invalid GDC file: expected bytecode version 100, found " + version);
            }

            var decompressedSize = compressedBinaryReader.ReadUInt32();
            Span<byte> decompressed = null;
            if (decompressedSize != 0)
            {
                var totalSize = compressedBinaryReader.BaseStream.Length - compressedBinaryReader.BaseStream.Position;
                decompressed = mZstdDecompressor.Unwrap(compressedBinaryReader.ReadBytes((int)totalSize));
            } else {
                throw new Exception("Invalid GDC file: missing decompressed size");
            }

            using var memoryStream = new MemoryStream(decompressed.ToArray());
            using var binaryReader = new BinaryReader(memoryStream);
            var identifierCount = binaryReader.ReadUInt32();
            var constantCount = binaryReader.ReadUInt32();
            var lineCount = binaryReader.ReadUInt32();
            binaryReader.ReadUInt32();
            var tokenCount = binaryReader.ReadUInt32();

            identifiers = new string[identifierCount];
            for (int i = 0; i < identifierCount; i++)
            {
                var stringLength = binaryReader.ReadUInt32();
                var stringData = binaryReader.ReadBytes((int)stringLength * 4);
                for (var j = 0; j < stringData.Length; j++)
                    stringData[j] ^= 0xB6;
                identifiers[i] = Encoding.UTF32.GetString(stringData);
            }

            constants = new IGdStructure[constantCount];
            for (int i = 0; i < constantCount; i++)
                constants[i] = DecodeConstant(binaryReader, provider);

            tokenLineMap = new Dictionary<uint, uint>((int)lineCount);
            var tokenColumnMap = new Dictionary<uint, uint>((int)lineCount);

            // Lines
            for (int i = 0; i < lineCount; i++) {
                var tokenIndex = binaryReader.ReadUInt32();
                var line = binaryReader.ReadUInt32();

                tokenLineMap.Add(tokenIndex, line);
            }

            // Columns
            for (int i = 0; i < lineCount; i++) {
                var tokenIndex = binaryReader.ReadUInt32();
                var column = binaryReader.ReadUInt32();

                tokenColumnMap.Add(tokenIndex, column);
            }

            tokens = new List<GdcToken>((int)tokenCount);
            

            var decompile = new DecompileBuffer();

            var currentLineIndex = 1;

            var lastLine = 1;
            var lastColumn = 1;

            for (int i = 0; i < tokenCount; i++)
            {
                uint tokenData = binaryReader.ReadByte();

                if ((tokenData & 0x80) != 0) {
                    binaryReader.BaseStream.Seek(-1, SeekOrigin.Current);
                    tokenData = binaryReader.ReadUInt32() ^ 0x80;
                }

                var line = binaryReader.ReadUInt32();

                if (tokenLineMap.TryGetValue((uint)i, out var updatedLineIndex))
                    currentLineIndex = (int)updatedLineIndex;

                // TEMP
                if (tokenLineMap.TryGetValue((uint)i, out var updatedLine))
                    lastLine = (int)updatedLine;

                if (tokenColumnMap.TryGetValue((uint)i, out var updatedColumn))
                    lastColumn = (int)updatedColumn;

                var token = new GdcToken() {
                    Type = provider.TokenTypeProvider.GetTokenType(tokenData & 0x7F),
                    Data = tokenData >> 8,
                    LineIndex = (uint)currentLineIndex,
                    Line = (uint)lastLine,
                    Column = (uint)lastColumn
                };

                tokens.Add(token);
                ReadToken(token, identifiers, constants);
            }

            GdcTokenType previous = GdcTokenType.Newline;

            lastLine = 1;
            foreach (GdcToken token in tokens)
            {
                if (token.Line != lastLine) {
                    decompile.Append("\n");
                    lastLine = (int)token.Line;
                    decompile.Append(new string(' ', (int)token.Column - 1));
                }


                token.Decompile(decompile, previous, provider);
                // decompile.Append($"{{{token.Line}:{token.Column}}}");
                previous = token.Type;
            }

            return decompile.Content;
        }

        public static string Decompile(byte[] arr, BytecodeProvider provider, bool debug = false)
        {
            using MemoryStream ms = new MemoryStream(arr);
            using BinaryReader buf = new BinaryReader(ms);

            string magicHeader = Encoding.ASCII.GetString(buf.ReadBytes(4));
            if (magicHeader != "GDSC")
            {
                throw new Exception("Invalid GDC file: missing magic header");
            }

            if (provider.ProviderData.BytecodeVersion >= 100)
            {
                return DecompileV2(provider, buf, out var identifiers1, out var constants1, out var tokens1, out var tokenLineMap1);
            }

            uint version = buf.ReadUInt32();
            if (version != provider.ProviderData.BytecodeVersion)
            {
                throw new Exception("Invalid GDC file: expected bytecode version " + provider.ProviderData.BytecodeVersion + ", found " + version);
            }

            uint identifierCount = buf.ReadUInt32();
            uint constantCount = buf.ReadUInt32();
            uint lineCount = buf.ReadUInt32();
            uint tokenCount = buf.ReadUInt32();

            Console.WriteLine("Identifiers: " + identifierCount);
            string[] identifiers = new string[identifierCount];
            for (int i = 0; i < identifierCount; i++)
            {
                uint len = buf.ReadUInt32();
                byte[] strBytes = new byte[len];
                for (int j = 0; j < len; j++)
                {
                    strBytes[j] = (byte)(buf.ReadByte() ^ 0xB6);
                }
                string ident = Encoding.UTF8.GetString(strBytes).Replace("\0", "");
                identifiers[i] = ident;
            }

            IGdStructure[] constants = new IGdStructure[constantCount];
            for (int i = 0; i < constantCount; i++)
            {
                constants[i] = DecodeConstant(buf, provider);
            }

            Dictionary<uint, uint> tokenLineMap = new Dictionary<uint, uint>((int)lineCount);
            for (int i = 0; i < lineCount; i++)
            {
                tokenLineMap.Add(buf.ReadUInt32(), buf.ReadUInt32());
            }

            DecompileBuffer decompile = new DecompileBuffer();
            List<GdcToken> tokens = new List<GdcToken>((int)tokenCount);
            for (int i = 0; i < tokenCount; i++)
            {
                byte cur = arr[buf.BaseStream.Position];

                uint tokenType;
                if ((cur & 0x80) != 0)
                {
                    tokenType = (uint)(buf.ReadUInt32() & ~0x80);
                }
                else
                {
                    tokenType = cur;
                    buf.BaseStream.Position += 1;
                }

                GdcToken token = new GdcToken
                {
                    Type = provider.TokenTypeProvider.GetTokenType(tokenType & 0xFF),
                    Data = tokenType >> 8
                };
                tokens.Add(token);
                ReadToken(token, identifiers, constants);
            }

            GdcTokenType previous = GdcTokenType.Newline;
            foreach (GdcToken token in tokens)
            {
                token.Decompile(decompile, previous, provider);
                if (debug)
                {
                }
                previous = token.Type;
            }

            return decompile.Content;
        }

        private static void ReadToken(GdcToken token, string[] identifiers, IGdStructure[] constants)
        {
                    try { 
            switch (token.Type)
            {
                case GdcTokenType.Annotation: goto case GdcTokenType.Identifier;
                case GdcTokenType.Identifier:
                    token.Operand = new GdcIdentifier(identifiers[token.Data]);
                    return;
                case GdcTokenType.Literal: goto case GdcTokenType.Constant;
                case GdcTokenType.Constant:
                    token.Operand = constants[token.Data];
                    return;
                default:
                    return;
            }
                    } catch (Exception e) {
                        token.Operand = new GdcNull();
                        Console.WriteLine(e);
    return;
                    }

        }

        private static IGdStructure DecodeConstant(BinaryReader buf, BytecodeProvider provider)
        {
            uint type = buf.ReadUInt32();
            string typeName = provider.TypeNameProvider.GetTypeName(type & 0xFF);

            switch (typeName)
            {
                case "Nil":
                    return new GdcNull().Deserialize(buf);
                case "bool":
                    return new GdcBool().Deserialize(buf);
                case "int":
                    if ((type & (1 << 16)) != 0)
                    {
                        return new GdcInt64().Deserialize(buf);
                    }
                    else
                    {
                        return new GdcInt32().Deserialize(buf);
                    }
                case "float":
                    if ((type & (1 << 16)) != 0)
                    {
                        return new GdcDouble().Deserialize(buf);
                    }
                    else
                    {
                        return new GdcSingle().Deserialize(buf);
                    }
                case "String":
                    return new GdcString().Deserialize(buf);
                case "Vector2":
                    return new Vector2().Deserialize(buf);
                case "Rect2":
                    return new Rect2().Deserialize(buf);
                case "Vector3":
                    return new Vector3().Deserialize(buf);
                case "Transform2D":
                    return new Transform2d().Deserialize(buf);
                case "Plane":
                    return new Plane().Deserialize(buf);
                case "Quat":
                    return new Quat().Deserialize(buf);
                case "AABB":
                    return new Aabb().Deserialize(buf);
                case "Basis":
                    return new Basis().Deserialize(buf);
                case "Transform":
                    return new Transform().Deserialize(buf);
                case "Color":
                    return new Color().Deserialize(buf);
                case "NodePath":
                    throw new NotImplementedException("NodePath");
                case "RID":
                    throw new NotImplementedException("RID");
                default:
                    throw new NotImplementedException(type.ToString());
            }
        }
    }
}
