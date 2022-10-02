using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LunarHelper
{
    using static Program;

    internal class Importer
    {
        static private string UBERASM_SUCCESS_STRING = "Codes inserted successfully.";

        static public bool ApplyPatch(Config config, string cleanROM, string outROM, string patchBPS)
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
                Log("No path to human-readable-map16-cli.exe provided, using binary map16 format", ConsoleColor.Red);
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

        static public bool ApplyAsarPatch(Config config, string patch_path)
        {
            Log($"Patch '{patch_path}'", ConsoleColor.Cyan);

            ProcessStartInfo psi = new ProcessStartInfo(config.AsarPath, $"{config.AsarOptions ?? ""} \"{patch_path}\" \"{config.TempPath}\"");

            var p = Process.Start(psi);
            p.WaitForExit();

            if (p.ExitCode == 0)
            {
                Log("Patch Success!\n", ConsoleColor.Green);
                return true;
            }
            else
            {
                Log("Patch Failure!\n", ConsoleColor.Red);
                return false;
            }
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
