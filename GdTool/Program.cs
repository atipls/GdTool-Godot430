using CommandLine;
using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Net;

namespace GdTool {
    public class Program {

        public static string HexDump(byte[] bytes, int bytesPerLine = 16) {
            if (bytes == null) return "<null>";
            int bytesLength = bytes.Length;

            char[] HexChars = "0123456789ABCDEF".ToCharArray();

            int firstHexColumn =
                  8                   // 8 characters for the address
                + 3;                  // 3 spaces

            int firstCharColumn = firstHexColumn
                + bytesPerLine * 3       // - 2 digit for the hexadecimal value and 1 space
                + (bytesPerLine - 1) / 8 // - 1 extra space every 8 characters from the 9th
                + 2;                  // 2 spaces 

            int lineLength = firstCharColumn
                + bytesPerLine           // - characters to show the ascii value
                + Environment.NewLine.Length; // Carriage return and line feed (should normally be 2)

            char[] line = (new String(' ', lineLength - Environment.NewLine.Length) + Environment.NewLine).ToCharArray();
            int expectedLines = (bytesLength + bytesPerLine - 1) / bytesPerLine;
            StringBuilder result = new StringBuilder(expectedLines * lineLength);

            for (int i = 0; i < bytesLength; i += bytesPerLine) {
                line[0] = HexChars[(i >> 28) & 0xF];
                line[1] = HexChars[(i >> 24) & 0xF];
                line[2] = HexChars[(i >> 20) & 0xF];
                line[3] = HexChars[(i >> 16) & 0xF];
                line[4] = HexChars[(i >> 12) & 0xF];
                line[5] = HexChars[(i >> 8) & 0xF];
                line[6] = HexChars[(i >> 4) & 0xF];
                line[7] = HexChars[(i >> 0) & 0xF];

                int hexColumn = firstHexColumn;
                int charColumn = firstCharColumn;

                for (int j = 0; j < bytesPerLine; j++) {
                    if (j > 0 && (j & 7) == 0) hexColumn++;
                    if (i + j >= bytesLength) {
                        line[hexColumn] = ' ';
                        line[hexColumn + 1] = ' ';
                        line[charColumn] = ' ';
                    } else {
                        byte b = bytes[i + j];
                        line[hexColumn] = HexChars[(b >> 4) & 0xF];
                        line[hexColumn + 1] = HexChars[b & 0xF];
                        line[charColumn] = (b < 32 ? '·' : (char)b);
                    }
                    hexColumn += 3;
                    charColumn++;
                }
                result.Append(line);
            }
            return result.ToString();
        }


        [Verb("decode", HelpText = "Decodes and extracts a PCK file.")]
        public class DecodeOptions {
            [Option('i', "in", Required = true, HelpText = "The PCK file to extract.")]
            public string InputPath { get; set; }

            [Option('b', "bytecode-version", HelpText = "The commit hash of the bytecode version to use.")]
            public string BytecodeVersion { get; set; }

            [Option('d', "decompile", Required = false, HelpText = "Decompiles GDC files when in decode mode.")]
            public bool Decompile { get; set; }

            [Option('o', "out", Required = false, HelpText = "Output directory to extract files to.")]
            public string OutputDirectory { get; set; }
        }

        [Verb("build", HelpText = "Packs a directory into a PCK file.")]
        public class BuildOptions {
            [Option('i', "in", Required = true, HelpText = "The directory to pack.")]
            public string InputPath { get; set; }

            [Option('b', "bytecode-version", HelpText = "The commit hash of the bytecode version to use.")]
            public string BytecodeVersion { get; set; }

            [Option('o', "out", Required = false, HelpText = "Output file to place the PCK.")]
            public string OutputFile { get; set; }
        }

        [Verb("detect", HelpText = "Detects information for a game executable without executing it.")]
        public class DetectVersionOptions {
            [Option('i', "in", Required = true, HelpText = "The game executable to probe information from.")]
            public string InputPath { get; set; }
        }

        static void Main(string[] args) {
            var customCulture = System.Globalization.CultureInfo.CreateSpecificCulture("en-US");
            System.Threading.Thread.CurrentThread.CurrentCulture = customCulture;
            if (Debugger.IsAttached) {
                // decode -d -b 77dcf97d82cbfe4e4615475fa52ca03da645dbd8 -i /Users/atipls/Downloads/Megaloot.exe
                args = new string[] { "decode", "-d", "-b", "77dcf97d82cbfe4e4615475fa52ca03da645dbd8", "-i", "/Users/atipls/Downloads/Megaloot.exe" };
            }
            Parser.Default.ParseArguments<DecodeOptions, BuildOptions, DetectVersionOptions>(args)
                .WithParsed<DecodeOptions>(Decode)
                .WithParsed<BuildOptions>(Build)
                .WithParsed<DetectVersionOptions>(DetectVersion);
        }

        private static void ProcessFile(PckFile pack, PckFileEntry file, DecodeOptions options, BytecodeProvider byteCodeProvider, string outputDirectory) {
            var fileName = file.Path.Replace("res://", "");
            var outputFileName = Path.Combine(outputDirectory, fileName);

            if (options.Decompile && fileName.EndsWith(".gdc")) {
                string decompiled = GdScriptDecompiler.Decompile(file.Data, byteCodeProvider);
                File.WriteAllText(outputFileName[..^1], decompiled);
                return;
            }

            if (fileName.EndsWith(".import")) {
                var importConfiguration = ConfigFile.Load(file.Data);
                if (!importConfiguration.TryGet("remap", "path", out var sourcePath))
                    importConfiguration.TryGet("remap", "path.s3tc", out sourcePath);
                sourcePath = sourcePath.Trim('"');

                var sourceFile = pack.FindByName(sourcePath);
                var destinationPath = Path.Join(outputDirectory, fileName[..^7]);

                if (sourcePath.EndsWith(".ctex")) {
                    // Console.WriteLine($"Decompressing texture: {sourcePath} -> {destinationPath}");
                    var texture = AssetDecompressor.DecompressTexture(sourceFile.Data);
                    importConfiguration.AddKey("deps", "source_file", '"' + fileName[..^7] + '"');
                    importConfiguration.AddKey("deps", "dest_files", $"[\"{sourcePath}]\"");

                    File.WriteAllText(outputFileName, importConfiguration.Serialize());
                    File.WriteAllBytes(destinationPath + ".webp", texture);
                    Process.Start("magick", [destinationPath + ".webp", destinationPath]).WaitForExit();
                    File.Delete(destinationPath + ".webp");
                    return;
                }

                if (sourcePath.EndsWith(".fontdata") || sourcePath.EndsWith(".sample") || sourcePath.EndsWith(".oggvorbisstr")) {
                    var asset = AssetDecompressor.DecompressResource(sourceFile.Data);
                    importConfiguration.AddKey("deps", "source_file", '"' + fileName[..^7] + '"');
                    importConfiguration.AddKey("deps", "dest_files", $"[\"{sourcePath}]\"");
                    File.WriteAllText(outputFileName, importConfiguration.Serialize());
                    File.WriteAllBytes(destinationPath, asset);
                    return;
                }


                Console.WriteLine($"Unknown import type: {sourcePath}");
                File.WriteAllBytes(outputFileName, file.Data[..^1]);
                return;
            }

            if (fileName.EndsWith(".ctex")) {
                return;
            }

            if (fileName.EndsWith(".remap")) {
                var remapConfiguration = ConfigFile.Load(file.Data);
                if (!remapConfiguration.TryGet("remap", "path", out var sourcePath))
                    remapConfiguration.TryGet("remap", "path.s3tc", out sourcePath);

                sourcePath = sourcePath.Replace(".gdc", ".gd");
                remapConfiguration.AddKey("remap", "path", sourcePath);
                File.WriteAllText(outputFileName, remapConfiguration.Serialize());
                return;
            }

            Console.WriteLine($"Extracting: {fileName}");
            File.WriteAllBytes(outputFileName, file.Data);
        }

        private static void Decode(DecodeOptions options) {
            if (!File.Exists(options.InputPath)) {
                Console.WriteLine("Invalid PCK file (does not exist): " + options.InputPath);
                return;
            }

            string fileName = Path.GetFileName(options.InputPath);
            if (fileName.Contains('.')) {
                fileName = fileName[..fileName.LastIndexOf('.')];
            }
            string outputDirectory;
            if (options.OutputDirectory != null) {
                outputDirectory = options.OutputDirectory;
            } else {
                outputDirectory = Path.Combine(Path.GetDirectoryName(options.InputPath), fileName);
                // if (Directory.Exists(outputDirectory)) {
                //     Console.Write("Output directory \"" + outputDirectory + "\" already exists. Do you want to overwrite? (y/n): ");
                //     if (Console.ReadLine().ToLower() != "y") {
                //         return;
                //     }
                // }
            }

            if (!Directory.Exists(outputDirectory)) {
                Directory.CreateDirectory(outputDirectory);
            }

            byte[] pckBytes = File.ReadAllBytes(options.InputPath);
            PckFile pck;
            try {
                Console.Write("Reading PCK file... ");
                pck = new PckFile(pckBytes);
            } catch (Exception e) {
                Console.WriteLine("invalid (could not parse).");
                Console.WriteLine(e);
                return;
            }

            Console.WriteLine("success.");

            var project = new GdToolProject {
                PackFormatVersion = pck.PackFormatVersion,
                VersionMajor = pck.VersionMajor,
                VersionMinor = pck.VersionMinor,
                VersionPatch = pck.VersionPatch
            };
            byte[] serializedProject = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(project));
            File.WriteAllBytes(Path.Combine(outputDirectory, "gdtool-project.json"), serializedProject);

            BytecodeProvider provider = null;
            if (options.Decompile) {
                if (options.BytecodeVersion == null) {
                    Console.WriteLine("Bytecode version must be supplied to decompile.");
                    return;
                }
                provider = BytecodeProvider.GetByCommitHash(options.BytecodeVersion);
            }

            for (int i = 0; i < pck.Entries.Count; i++) {
                PckFileEntry entry = pck.Entries[i];
                string path = entry.Path[6..];

                if (path.Contains("project.binary"))
                    continue;

                try {
                    string parent = Path.GetDirectoryName(Path.Combine(outputDirectory, path));
                    if (!Directory.Exists(parent))
                        Directory.CreateDirectory(parent);

                    ProcessFile(pck, entry, options, provider, outputDirectory);

                    int percentage = (int)Math.Floor((i + 1) / (double)pck.Entries.Count * 100.0);
                    Console.Write("\rUnpacking: " + (i + 1) + "/" + pck.Entries.Count + " (" + percentage + "%)");
                } catch (Exception e) {
                    Console.WriteLine("\nError while decoding file: " + path);
                    Console.WriteLine(e);
                    Environment.Exit(1);
                }
            }

            var projectBinary = pck.FindByName("res://project.binary");
            var projectConfiguration = ConfigFile.LoadBinary(projectBinary.Data);

            File.WriteAllText(Path.Combine(outputDirectory, "project.godot"), "config_version=5\n\n" + projectConfiguration.Serialize());
            Console.WriteLine();
        }

        private static void Build(BuildOptions options) {
            if (!Directory.Exists(options.InputPath)) {
                Console.WriteLine("Invalid directory (does not exist): " + options.InputPath);
                return;
            }

            if (!File.Exists(Path.Combine(options.InputPath, "project.binary"))) {
                Console.WriteLine("Invalid project (project.binary file not present in directory): " + options.InputPath);
                return;
            }

            if (!File.Exists(Path.Combine(options.InputPath, "gdtool-project.json"))) {
                Console.WriteLine("Invalid project (gdtool-project.json file not present in directory): " + options.InputPath);
                return;
            }

            string serializedProject = Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(options.InputPath, "gdtool-project.json")));
            GdToolProject project = JsonConvert.DeserializeObject<GdToolProject>(serializedProject);

            PckFile pck = new PckFile(project.PackFormatVersion, project.VersionMajor, project.VersionMinor, project.VersionPatch);
            BytecodeProvider provider = null;
            if (options.BytecodeVersion != null) {
                provider = BytecodeProvider.GetByCommitHash(options.BytecodeVersion);
            }
            string[] files = Directory.GetFiles(options.InputPath, "*", SearchOption.AllDirectories);
            pck.Entries.Capacity = files.Length;
            for (int i = 0; i < files.Length; i++) {
                string file = files[i];
                string relative = Path.GetRelativePath(options.InputPath, file);
                if (relative.Equals("gdtool-project.json")) { // don't pack the project fuke
                    continue;
                }
                try {
                    string withPrefix = "res://" + relative.Replace('\\', '/');
                    byte[] contents = File.ReadAllBytes(file);
                    if (relative.EndsWith(".gd")) {
                        if (provider == null) {
                            Console.WriteLine("Bytecode version must be supplied to compile GdScript file: " + relative);
                            return;
                        }
                        contents = GdScriptCompiler.Compile(Encoding.UTF8.GetString(contents), provider);
                        withPrefix += "c"; // convert ".gd" to ".gdc"
                    }
                    pck.Entries.Add(new PckFileEntry {
                        Path = withPrefix,
                        Data = contents
                    });

                    int percentage = (int)Math.Floor((i + 1) / (double)files.Length * 100.0);
                    Console.Write("\rPacking: " + (i + 1) + "/" + files.Length + " (" + percentage + "%)");
                } catch (Exception e) {
                    Console.WriteLine("\nError while building file: " + relative);
                    Console.WriteLine(e);
                    Environment.Exit(1);
                }
            }

            Console.WriteLine();

            byte[] serialized = pck.ToBytes();
            string outputFile = options.InputPath + ".pck";
            if (options.OutputFile != null) {
                outputFile = options.OutputFile;
            }
            Console.WriteLine("Writing PCK file to disk... ");
            File.WriteAllBytes(outputFile, serialized);
        }

        private static void DetectVersion(DetectVersionOptions options) {
            if (!File.Exists(options.InputPath)) {
                Console.WriteLine("Invalid game executable file (does not exist): " + options.InputPath);
                return;
            }

            byte[] binary = File.ReadAllBytes(options.InputPath);
            BytecodeProvider provider = VersionDetector.Detect(binary);
            if (provider == null) {
                Console.WriteLine("A known commit hash could not be found within the binary. Are you sure you supplied a Godot game executable?");
                Console.WriteLine("If you definitely passed a valid executable, it might be compiled with a version newer than this build of GdTool.");
                Console.WriteLine("If this is the case, try compiling with the newest bytecode version GdTool supports if it's still compatible.");
                return;
            }

            Console.WriteLine("Bytecode version hash: " + provider.ProviderData.CommitHash.Substring(0, 7) + " (" + provider.ProviderData.CommitHash + ")");
            Console.WriteLine(provider.ProviderData.Description);
        }
    }
}
