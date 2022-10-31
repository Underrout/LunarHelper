using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using AsarCLR;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace LunarHelper
{
    using static Program;

    internal class Importer
    {
        static private string UBERASM_SUCCESS_STRING = "Codes inserted successfully.";

        static public bool ApplyBpsPatch(Config config, string cleanROM, string outROM, string patchBPS)
        {
            Log($"Patching {cleanROM}\n\tto {outROM}\n\twith {patchBPS}", ConsoleColor.Yellow);

            Console.ForegroundColor = ConsoleColor.Yellow;
            var psi = new ProcessStartInfo(config.FlipsPath,
                    $"--apply \"{patchBPS}\" \"{cleanROM}\" \"{outROM}\"");
            var p = Process.Start(psi);
            p.WaitForExit();

            if (p.ExitCode == 0)
                return true;
            else
                return false;
        }

        static public bool ImportGlobalData(Config config)
        {
            Log("Global Data", ConsoleColor.Cyan);

            ProcessStartInfo psi;
            Process p;
            string globalDataROMPath = Path.Combine(
                Path.GetFullPath(Path.GetDirectoryName(config.GlobalDataPath)),
                Path.GetFileNameWithoutExtension(config.GlobalDataPath) + ".smc");

            //Apply patch to clean ROM
            {
                var fullPatchPath = Path.GetFullPath(config.GlobalDataPath);
                var fullCleanPath = Path.GetFullPath(config.CleanPath);

                Console.ForegroundColor = ConsoleColor.Yellow;
                psi = new ProcessStartInfo(config.FlipsPath,
                        $"--apply \"{fullPatchPath}\" \"{fullCleanPath}\" \"{globalDataROMPath}\"");
                p = Process.Start(psi);
                p.WaitForExit();

                if (p.ExitCode == 0)
                    Log("Global Data Patch Success!", ConsoleColor.Green);
                else
                {
                    Log("Global Data Patch Failure!", ConsoleColor.Red);
                    return false;
                }
            }

            //Overworld
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                psi = new ProcessStartInfo(config.LunarMagicPath,
                            $"-TransferOverworld \"{config.TempPath}\" \"{globalDataROMPath}\"");
                p = Process.Start(psi);
                p.WaitForExit();

                if (p.ExitCode == 0)
                    Log("Overworld Import Success!", ConsoleColor.Green);
                else
                {
                    Log("Overworld Import Failure!", ConsoleColor.Red);
                    return false;
                }
            }

            //Global EX Animations
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                psi = new ProcessStartInfo(config.LunarMagicPath,
                            $"-TransferLevelGlobalExAnim \"{config.TempPath}\" \"{globalDataROMPath}\"");
                p = Process.Start(psi);
                p.WaitForExit();

                if (p.ExitCode == 0)
                    Log("Global EX Animation Import Success!", ConsoleColor.Green);
                else
                {
                    Log("Global EX Animation Import Failure!", ConsoleColor.Red);
                    return false;
                }
            }

            //Title Screen
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                psi = new ProcessStartInfo(config.LunarMagicPath,
                            $"-TransferTitleScreen \"{config.TempPath}\" \"{globalDataROMPath}\"");
                p = Process.Start(psi);
                p.WaitForExit();

                if (p.ExitCode == 0)
                    Log("Title Screen Import Success!", ConsoleColor.Green);
                else
                {
                    Log("Title Screen Import Failure!", ConsoleColor.Red);
                    return false;
                }
            }

            //Credits
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                psi = new ProcessStartInfo(config.LunarMagicPath,
                            $"-TransferCredits \"{config.TempPath}\" \"{globalDataROMPath}\"");
                p = Process.Start(psi);
                p.WaitForExit();

                if (p.ExitCode == 0)
                    Log("Credits Import Success!", ConsoleColor.Green);
                else
                {
                    Log("Credits Import Failure!", ConsoleColor.Red);
                    return false;
                }
            }

            if (File.Exists(globalDataROMPath))
                File.Delete(globalDataROMPath);

            Log("All Global Data Imported!", ConsoleColor.Green);
            Console.WriteLine();

            return true;
        }

        static public bool ImportSharedPalettes(Config config)
        {
            Log("Shared Palette", ConsoleColor.Cyan);

            Console.ForegroundColor = ConsoleColor.Yellow;
            ProcessStartInfo psi = new ProcessStartInfo(config.LunarMagicPath,
                        $"-ImportSharedPalette \"{config.TempPath}\" \"{config.SharedPalettePath}\"");
            var p = Process.Start(psi);
            p.WaitForExit();

            if (p.ExitCode == 0)
                Log("Shared Palette Import Success!", ConsoleColor.Green);
            else
            {
                Log("Shared Palette Import Failure!", ConsoleColor.Red);
                return false;
            }

            Console.WriteLine();

            return true;
        }

        static public bool ImportTitleMoves(Config config)
        {
            Log("Title Moves", ConsoleColor.Cyan);

            Console.ForegroundColor = ConsoleColor.Yellow;
            ProcessStartInfo psi = new ProcessStartInfo(config.LunarMagicPath,
                        $"-ImportTitleMoves \"{config.TempPath}\" \"{config.TitleMovesPath}\"");
            var p = Process.Start(psi);
            p.WaitForExit();

            if (p.ExitCode == 0)
                Log("Title Moves Import Success!", ConsoleColor.Green);
            else
            {
                Log("Title Moves Import Failure!", ConsoleColor.Red);
                return false;
            }

            Console.WriteLine();

            return true;
        }

        static public bool ImportMap16(Config config)
        {
            Log("Map16", ConsoleColor.Cyan);

            if (string.IsNullOrWhiteSpace(config.HumanReadableMap16CLI))
                Log("No path to human-readable-map16-cli.exe provided, using binary map16 format", ConsoleColor.Yellow);
            else
            {
                string humanReadableDirectory;
                if (!string.IsNullOrWhiteSpace(config.HumanReadableMap16Directory))
                    humanReadableDirectory = config.HumanReadableMap16Directory;
                else
                    humanReadableDirectory = Path.Combine(Path.GetDirectoryName(config.Map16Path), Path.GetFileNameWithoutExtension(config.Map16Path));

                Console.ForegroundColor = ConsoleColor.Yellow;
                ProcessStartInfo process = new ProcessStartInfo(config.HumanReadableMap16CLI,
                            $"--to-map16 \"{humanReadableDirectory}\" \"{config.Map16Path}\"");
                var proc = Process.Start(process);
                proc.WaitForExit();

                if (proc.ExitCode == 0)
                {
                    Log("Human Readable Map16 Conversion Success!", ConsoleColor.Green);
                }
                else
                {
                    Log("Human Readable Map16 Conversion Failure!", ConsoleColor.Red);
                    return false;
                }
            }

            Console.ForegroundColor = ConsoleColor.Yellow;
            ProcessStartInfo psi = new ProcessStartInfo(config.LunarMagicPath,
                        $"-ImportAllMap16 \"{config.TempPath}\" \"{config.Map16Path}\"");
            var p = Process.Start(psi);
            p.WaitForExit();

            if (p.ExitCode == 0)
            {
                Log("Map16 Import Success!", ConsoleColor.Green);
                Console.WriteLine();
                return true;
            }
            else
            {
                Log("Map16 Import Failure!", ConsoleColor.Red);
                return false;
            }
        }

        static public bool ApplyGPS(Config config)
        {
            Log("GPS", ConsoleColor.Cyan);

            var dir = Path.GetFullPath(Path.GetDirectoryName(config.GPSPath));
            var rom = Path.GetRelativePath(dir, Path.GetFullPath(config.TempPath));
            Console.ForegroundColor = ConsoleColor.Yellow;

            ProcessStartInfo psi = new ProcessStartInfo(config.GPSPath, $"-l \"{dir}/list.txt\" {config.GPSOptions ?? ""} \"{rom}\"");
            psi.RedirectStandardInput = true;
            psi.WorkingDirectory = dir;

            var p = Process.Start(psi);
            p.WaitForExit();

            if (p.ExitCode == 0)
                Log("GPS Success!", ConsoleColor.Green);
            else
            {
                Log("GPS Failure!", ConsoleColor.Red);
                return false;
            }

            Console.WriteLine();

            return true;
        }

        static public bool ApplyAddmusicK(Config config)
        {
            Log("AddmusicK", ConsoleColor.Cyan);
            var dir = Path.GetFullPath(Path.GetDirectoryName(config.AddMusicKPath));

            // create bin folder if missing
            {
                string bin = Path.Combine(dir, "asm", "SNES", "bin");
                if (!Directory.Exists(bin))
                    Directory.CreateDirectory(bin);
            }

            var rom = Path.GetRelativePath(dir, Path.GetFullPath(config.TempPath));
            Console.ForegroundColor = ConsoleColor.Yellow;

            ProcessStartInfo psi = new ProcessStartInfo(config.AddMusicKPath, $" -noblock {config.AddmusicKOptions ?? ""} \"{rom}\"");
            psi.RedirectStandardInput = true;
            psi.WorkingDirectory = dir;

            var p = Process.Start(psi);
            p.WaitForExit();

            if (p.ExitCode == 0)
                Log("AddmusicK Success!", ConsoleColor.Green);
            else
            {
                Log("AddmusicK Failure!", ConsoleColor.Red);
                return false;
            }

            Console.WriteLine();

            return true;
        }

        static public bool ApplyUberASM(Config config)
        {
            Log("UberASM", ConsoleColor.Cyan);
            var dir = Path.GetFullPath(Path.GetDirectoryName(config.UberASMPath));

            // create work folder if missing
            {
                string bin = Path.Combine(dir, "asm", "work");
                if (!Directory.Exists(bin))
                    Directory.CreateDirectory(bin);
            }

            var rom = Path.GetRelativePath(dir, Path.GetFullPath(config.TempPath));
            Console.ForegroundColor = ConsoleColor.Yellow;

            ProcessStartInfo psi = new ProcessStartInfo(config.UberASMPath, $"{config.UberASMOptions ?? "list.txt"} \"{rom}\"");
            psi.RedirectStandardInput = true;
            psi.RedirectStandardOutput = true;
            psi.WorkingDirectory = dir;

            StringBuilder uberasm_output = new StringBuilder();

            var p = Process.Start(psi);
            p.OutputDataReceived += (sender, args) => {
                uberasm_output.AppendLine(args.Data);
                Log(args.Data, ConsoleColor.Yellow);
            };
            p.BeginOutputReadLine();
            p.WaitForExit();

            string output = uberasm_output.ToString();

            if (output.Contains(UBERASM_SUCCESS_STRING))
                Log("UberASM Success!", ConsoleColor.Green);
            else
            {
                Log("UberASM Failure!", ConsoleColor.Red);
                return false;
            }

            Console.WriteLine();

            return true;
        }

        static public bool ApplyPixi(Config config)
        {
            Log("PIXI", ConsoleColor.Cyan);

            var dir = Path.GetFullPath(Path.GetDirectoryName(config.PixiPath));
            Console.ForegroundColor = ConsoleColor.Yellow;

            ProcessStartInfo psi = new ProcessStartInfo(config.PixiPath, $"{config.PixiOptions ?? ""} \"{config.TempPath}\"");
            psi.RedirectStandardInput = true;

            var p = Process.Start(psi);
            p.WaitForExit();

            if (p.ExitCode == 0)
                Log("Pixi Success!", ConsoleColor.Green);
            else
            {
                Log("Pixi Failure!", ConsoleColor.Red);
                return false;
            }

            Console.WriteLine();
            return true;
        }

        static public bool ImportGraphics(Config config)
        {
            Log("Graphics", ConsoleColor.Cyan);
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                ProcessStartInfo psi = new ProcessStartInfo(config.LunarMagicPath,
                            $"-ImportGFX \"{config.TempPath}\"");
                var p = Process.Start(psi);
                p.WaitForExit();

                if (p.ExitCode == 0)
                    Log("Import Graphics Success!", ConsoleColor.Green);
                else
                {
                    Log("Import Graphics Failure!", ConsoleColor.Red);
                    return false;
                }

                Console.WriteLine();
                return true;
            }
        }

        static public bool ImportSingleLevel(Config config, string level_path)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            ProcessStartInfo psi = new ProcessStartInfo(config.LunarMagicPath,
                        $"-ImportLevel \"{config.TempPath}\" \"{level_path}\"");
            var p = Process.Start(psi);
            p.WaitForExit();

            if (p.ExitCode != 0)
            {
                Log($"Level import failure on '{level_path}'!", ConsoleColor.Red);
                return false;
            }

            return true;
        }

        static public bool ImportExGraphics(Config config)
        {
            Log("ExGraphics", ConsoleColor.Cyan);
            Console.ForegroundColor = ConsoleColor.Yellow;
            ProcessStartInfo psi = new ProcessStartInfo(config.LunarMagicPath,
                        $"-ImportExGFX \"{config.TempPath}\"");
            var p = Process.Start(psi);
            p.WaitForExit();

            if (p.ExitCode == 0)
                Log("Import ExGraphics Success!", ConsoleColor.Green);
            else
            {
                Log("Import ExGraphics Failure!", ConsoleColor.Red);
                return false;
            }

            Console.WriteLine();
            return true;
        }

        private static (byte[], byte[]) GetRomHeaderAndData(string rom_path)
        {
            byte[] full_rom = File.ReadAllBytes(rom_path);
            var header_size = full_rom.Length & 0x7FFF;
            byte[] rom = full_rom[header_size..];
            byte[] header = full_rom[0..header_size];

            return (rom, header);
        }

        private static void WriteGlobuleImprint(string globule_name, Asarlabel[] labels)
        {
            if (!Directory.Exists(".lunar_helper/globules"))
                Directory.CreateDirectory(".lunar_helper/globules");

            var imprint_path = $".lunar_helper/globules/{globule_name}";

            var globule_stream = new StreamWriter(imprint_path, false);

            globule_stream.WriteLine("incsrc \"../call_globule.asm\"\n");

            foreach (var label in labels)
            {
                if (label.Name.StartsWith(':'))
                    continue;

                var prefixed_label = Path.GetFileNameWithoutExtension(globule_name).Replace(' ', '_') + '_' + label.Name;
                var prefixed_label_as_define = '!' + prefixed_label;
                var location = Convert.ToString(label.Location, 16).ToUpper();

                globule_stream.WriteLine($"{prefixed_label} = ${location}");
                globule_stream.WriteLine($"{prefixed_label_as_define} = ${location}");
            }

            globule_stream.Close();
        }

        public static void FinalizeGlobuleImprints()
        {
            if (Directory.Exists(".lunar_helper/inserted_globules"))
                Directory.Delete(".lunar_helper/inserted_globules", true);

            Directory.CreateDirectory(".lunar_helper/globules");

            WriteCallGlobuleMacroFile();

            Microsoft.VisualBasic.FileIO.FileSystem.CopyDirectory(".lunar_helper/globules", ".lunar_helper/inserted_globules");
        }

        private static void WriteCallGlobuleMacroFile()
        {
            File.WriteAllText(".lunar_helper/call_globule.asm",
                "includeonce\n\nmacro call_globule(globule_label)\n\tPHB\n\t" +
                "LDA.b #<globule_label>>>16\n\tPHA\n\tPLB\n\tJSL <globule_label>\n\tPLB\nendmacro\n"
            );
        }

        public static void CopyGlobuleImprints(string[] globule_names_to_copy)
        {
            foreach (string globule_name in globule_names_to_copy)
            {
                File.Copy($".lunar_helper/inserted_globules/{globule_name}.asm", $".lunar_helper/globules/{globule_name}.asm", true);
            }
        }

        public static void ClearGlobuleFolder()
        {
            if (Directory.Exists(".lunar_helper/globules"))
                Directory.Delete(".lunar_helper/globules", true);

            Directory.CreateDirectory(".lunar_helper/globules");
        }

        public static bool CleanGlobules(string temp_rom_path, string[] globule_names_to_clean)
        {
            var clean_patch_path = Path.GetTempFileName();
            var clean_patch_stream = new StreamWriter(clean_patch_path);

            foreach (var globule_name in globule_names_to_clean)
            {
                var globule_path = $".lunar_helper/inserted_globules/{globule_name}.asm";
                if (!File.Exists(globule_path))
                    return false;

                var reader = new StreamReader(globule_path);
                var line = reader.ReadLine();

                if (line == null)
                    return false;

                while (line != null)
                {
                    if (line.StartsWith('!'))
                    {
                        line = reader.ReadLine();
                        continue;  // skip over the define duplicates of the labels, it'd be redundant to clean both
                    }

                    var address_start = line.IndexOf('$');

                    if (address_start == -1)
                    {
                        line = reader.ReadLine();
                        continue;
                    }

                    var hex_address = line.Substring(address_start);

                    clean_patch_stream.WriteLine($"autoclean {hex_address}");

                    line = reader.ReadLine();
                }

                var prev_imprint = $".lunar_helper/globules/{globule_name}.asm";

                if (File.Exists(prev_imprint))
                    File.Delete(prev_imprint);
            }

            clean_patch_stream.Close();

            (var rom, var header) = GetRomHeaderAndData(temp_rom_path);

            var res = Asar.patch(clean_patch_path, ref rom);

            if (!res)
            {
                foreach (var error in Asar.geterrors())
                    Log(error.Fullerrdata, ConsoleColor.Yellow);

                Log($"Globule Cleanup Failure!\n", ConsoleColor.Red);

                return false;
            }

            using var rom_stream = new FileStream(temp_rom_path, FileMode.Truncate);
            rom_stream.Write(header);
            rom_stream.Write(rom);

            return true;
        }

        public static bool ApplyAllGlobules(string temp_rom_path, string globules_path)
        {
            if (!Directory.Exists(globules_path))
            {
                Error($"Globules folder '{globules_path}' not found!");
                return false;
            }

            foreach (var globule_path in Directory.EnumerateFiles(globules_path, "*.asm", SearchOption.TopDirectoryOnly))
            {
                var res = ApplyGlobule(temp_rom_path, globule_path);

                if (!res)
                    return false;
            }

            return true;
        }

        public static bool ApplyGlobule(string temp_rom_path, string globule_path)
        {
            Log($"Globule '{globule_path}'", ConsoleColor.Cyan);

            var temp_patch_path = Path.GetTempFileName();

            var temp_patch_stream = new StreamWriter(temp_patch_path);
            temp_patch_stream.WriteLine("warnings disable W1011");  // any freespace used in the globule is cleaned by LH anyway, no need to warn about "leaks"
            temp_patch_stream.WriteLine("freecode cleaned");
            temp_patch_stream.WriteLine($"incsrc \"{Path.GetFullPath(globule_path).Replace('\\', '/')}\"");
            temp_patch_stream.Close();

            (var rom, var header) = GetRomHeaderAndData(temp_rom_path);
            var res = Asar.patch(temp_patch_path, ref rom);

            File.Delete(temp_patch_path);

            foreach (var print in Asar.getprints())
            {
                Log(print);
            }

            foreach (var warning in Asar.getwarnings())
            {
                Log(warning.Fullerrdata, ConsoleColor.Yellow);
            }

            if (!res)
            {
                foreach (var error in Asar.geterrors())
                    Log(error.Fullerrdata, ConsoleColor.Yellow);

                Log($"Globule Insertion Failure!\n", ConsoleColor.Red);

                return false;
            }

            using var rom_stream = new FileStream(temp_rom_path, FileMode.Truncate);
            rom_stream.Write(header);
            rom_stream.Write(rom);

            WriteGlobuleImprint(Path.GetFileName(globule_path), Asar.getlabels());

            Log($"Globule Insertion Success!\n", ConsoleColor.Green);

            return true;
        }

        public static bool ApplyAsarPatch(Config config, string patch_path)
        {
            Log($"Patch '{patch_path}'", ConsoleColor.Cyan);

            (var rom, var header) = GetRomHeaderAndData(config.TempPath);

            var res = Asar.patch(Path.GetFullPath(patch_path), ref rom);

            foreach (var print in Asar.getprints())
            {
                Log(print);
            }

            foreach (var warning in Asar.getwarnings())
            {
                Log(warning.Fullerrdata, ConsoleColor.Yellow);
            }

            if (!res)
            {
                foreach (var error in Asar.geterrors())
                    Log(error.Fullerrdata, ConsoleColor.Yellow);

                Log($"Patching Failure!\n", ConsoleColor.Red);

                return false;
            }

            using var rom_stream = new FileStream(config.TempPath, FileMode.Truncate);
            rom_stream.Write(header);
            rom_stream.Write(rom);

            Log($"Patching Success!\n", ConsoleColor.Green);

            return true;
        }

        static public bool ImportAllLevels(Config config)
        {
            Log("Levels", ConsoleColor.Cyan);
            // import levels
            Console.ForegroundColor = ConsoleColor.Yellow;
            ProcessStartInfo psi = new ProcessStartInfo(config.LunarMagicPath,
                        $"-ImportMultLevels \"{config.TempPath}\" \"{config.LevelsPath}\" {config.LunarMagicLevelImportFlags ?? ""}");
            var p = Process.Start(psi);
            p.WaitForExit();

            if (p.ExitCode == 0)
                Log("Levels Import Success!", ConsoleColor.Green);
            else
            {
                Log("Levels Import Failure!", ConsoleColor.Red);
                return false;
            }

            Console.WriteLine();

            return true;
        }
    }
}
