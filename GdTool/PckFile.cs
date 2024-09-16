using System;
using System.Text;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using ZstdSharp;
using System.IO.Compression;

namespace GdTool
{
    public class PckFileEntry
    {
        public string Path;
        public byte[] Data;
    }

    enum FileCompressionMode
    {
        MODE_FASTLZ,
        MODE_DEFLATE,
        MODE_ZSTD,
        MODE_GZIP,
        MODE_BROTLI
    };

    public class PckFile
    {
        public uint PackFormatVersion;
        public uint VersionMajor;
        public uint VersionMinor;
        public uint VersionPatch;

        public int Flags = -1;
        public long FileBase = 0;
        public long FileBaseAddressOffset = 0;
        public bool Embedded = false;
        public List<PckFileEntry> Entries;

        public long StartPosition = 0;
        public long EndPosition = 0;

        public const int PCK_DIR_ENCRYPTED = 1 << 0;
        public const int PCK_FILE_ENCRYPTED = 1 << 0;

        public PckFile(uint packFormatVersion, uint versionMajor, uint versionMinor, uint versionPatch)
        {
            PackFormatVersion = packFormatVersion;
            VersionMajor = versionMajor;
            VersionMinor = versionMinor;
            VersionPatch = versionPatch;
            Entries = [];
        }

        public PckFileEntry FindByName(string name)
        {
            return Entries.Find(entry => entry.Path == name);
        }

        public PckFile(byte[] arr)
        {
            using var memoryStream = new MemoryStream(arr);
            using var binaryReader = new BinaryReader(memoryStream);
            using var decompressor = new Decompressor();


            string magicHeader = Encoding.ASCII.GetString(binaryReader.ReadBytes(4));
            if (magicHeader != "GDPC")
            {
                memoryStream.Seek(-4, SeekOrigin.End);
                magicHeader = Encoding.ASCII.GetString(binaryReader.ReadBytes(4));
                if (magicHeader != "GDPC")
                {
                    throw new Exception("Invalid PCK file: missing magic header");
                }
                memoryStream.Seek(-12, SeekOrigin.Current);

                long ds = binaryReader.ReadInt64();
                memoryStream.Seek(-ds - 8, SeekOrigin.Current);

                magicHeader = Encoding.ASCII.GetString(binaryReader.ReadBytes(4));
                if (magicHeader != "GDPC")
                {
                    binaryReader.Close();
                    throw new Exception("Invalid PCK file: missing magic header");
                }
                else
                {
                    Embedded = true;
                    StartPosition = memoryStream.Position - 4;
                    EndPosition = memoryStream.Length - 12;
                }
            }
            else
            {
                StartPosition = 0;
                EndPosition = memoryStream.Length;
            }

            PackFormatVersion = binaryReader.ReadUInt32();
            VersionMajor = binaryReader.ReadUInt32();
            VersionMinor = binaryReader.ReadUInt32();
            VersionPatch = binaryReader.ReadUInt32();

            Flags = 0;
            FileBase = 0;

            if (PackFormatVersion >= 2)
            {
                Flags = binaryReader.ReadInt32();
                FileBaseAddressOffset = memoryStream.Position;
                FileBase = binaryReader.ReadInt64();
            }

            binaryReader.BaseStream.Position += 4 * 16;

            uint filesCount = binaryReader.ReadUInt32();

            if ((Flags & 2) != 0)
            {
                // RELATIVE FILE BASE
                FileBase += StartPosition;
            }

            Entries = new List<PckFileEntry>((int)filesCount);
            for (int i = 0; i < filesCount; i++)
            {
                var pathLength = binaryReader.ReadInt32();
                var path = Encoding.UTF8.GetString(binaryReader.ReadBytes(pathLength)).Replace("\0", "");
                var fileOffset = binaryReader.ReadInt64() + FileBase;
                var fileLength = binaryReader.ReadInt64();
                byte[] md5 = binaryReader.ReadBytes(16);

                int flags = 0;
                if (PackFormatVersion >= 2)
                {
                    flags = binaryReader.ReadInt32();
                }

                long pos = binaryReader.BaseStream.Position;
                binaryReader.BaseStream.Position = (long)fileOffset;

                var fileData = binaryReader.ReadBytes((int)fileLength);

                Entries.Add(new PckFileEntry
                {
                    Path = path,
                    Data = fileData
                });

                binaryReader.BaseStream.Position = pos;
            }
        }

        public byte[] ToBytes()
        {
            int totalSize =
                4 + // magic header
                4 * 4 + // version info
                4 * 16 + // padding
                4 + // files count
                Entries.Select(entry =>
                    4 + // path length prefix
                    Encoding.UTF8.GetBytes(entry.Path).Length + // size of path
                    8 * 2 + // offset and size
                    16 + // md5 hash
                    entry.Data.Length // file bytes
                ).Sum();
            byte[] arr = new byte[totalSize];
            using var ms = new MemoryStream(arr);
            using var buf = new BinaryWriter(ms);

            buf.Write(Encoding.ASCII.GetBytes("GDPC"));
            buf.Write(PackFormatVersion);
            buf.Write(VersionMajor);
            buf.Write(VersionMinor);
            buf.Write(VersionPatch);
            buf.Write(new byte[4 * 16]);
            buf.Write((uint)Entries.Count);

            long[] fileOffsets = new long[Entries.Count];

            for (int i = 0; i < Entries.Count; i++)
            {
                PckFileEntry entry = Entries[i];
                byte[] pathBytes = Encoding.UTF8.GetBytes(entry.Path);
                buf.Write((uint)pathBytes.Length);
                buf.Write(pathBytes);
                fileOffsets[i] = buf.BaseStream.Position;
                buf.Write(0UL);
                buf.Write((ulong)entry.Data.Length);
                buf.Write(MD5.HashData(entry.Data));
            }

            for (int i = 0; i < Entries.Count; i++)
            {
                long curPos = buf.BaseStream.Position;
                buf.BaseStream.Position = fileOffsets[i];
                buf.Write((ulong)curPos);
                buf.BaseStream.Position = curPos;

                PckFileEntry entry = Entries[i];
                buf.Write(entry.Data);
            }

            return arr;
        }
    }
}
