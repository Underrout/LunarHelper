﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LunarHelper
{
    using static Program;

    internal class Exporter
    {
        static public bool ExportAll(Config config)
        {
            if (!File.Exists(config.OutputPath))
            {
                Log("Output ROM does not exist! Build first!", ConsoleColor.Red);
                return false;
            }

            // save global data
            Log("Saving Global Data BPS...", ConsoleColor.Cyan);
            if (string.IsNullOrWhiteSpace(config.GlobalDataPath))
                Log("No path for GlobalData BPS provided!", ConsoleColor.Red);
            else if (string.IsNullOrWhiteSpace(config.CleanPath))
                Log("No path for Clean ROM provided!", ConsoleColor.Red);
            else if (!File.Exists(config.CleanPath))
                Log("Clean ROM does not exist!", ConsoleColor.Red);
            else if (string.IsNullOrWhiteSpace(config.FlipsPath))
                Log("No path to Flips provided!", ConsoleColor.Red);
            else if (!File.Exists(config.FlipsPath))
                Log("Flips not found at the provided path!", ConsoleColor.Red);
            else
            {
                if (File.Exists(config.GlobalDataPath))
                    File.Delete(config.GlobalDataPath);

                var fullCleanPath = Path.GetFullPath(config.CleanPath);
                var fullOutputPath = Path.GetFullPath(config.OutputPath);
                var fullPackagePath = Path.GetFullPath(config.GlobalDataPath);
                if (CreatePatch(config, fullCleanPath, fullOutputPath, fullPackagePath))
                    Log("Global Data Patch Success!", ConsoleColor.Green);
                else
                {
                    Log("Global Data Patch Failure!", ConsoleColor.Red);
                    return false;
                }
            }

            // export map16
            Log("Exporting Map16...", ConsoleColor.Cyan);
            if (string.IsNullOrWhiteSpace(config.Map16Path))
                Log("No path for Map16 provided!", ConsoleColor.Red);
            else if (string.IsNullOrWhiteSpace(config.LunarMagicPath))
                Log("No Lunar Magic Path provided!", ConsoleColor.Red);
            else if (!File.Exists(config.LunarMagicPath))
                Log("Could not find Lunar Magic at the provided path!", ConsoleColor.Red);
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                ProcessStartInfo psi = new ProcessStartInfo(config.LunarMagicPath,
                            $"-ExportAllMap16 \"{config.OutputPath}\" \"{config.Map16Path}\"");
                var p = Process.Start(psi);
                p.WaitForExit();

                if (p.ExitCode == 0)
                    Log("Map16 Export Success!", ConsoleColor.Green);
                else
                {
                    Log("Map16 Export Failure!", ConsoleColor.Red);
                    return false;
                }

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
                                $"--from-map16 \"{config.Map16Path}\" \"{humanReadableDirectory}\"");
                    var proc = Process.Start(process);
                    proc.WaitForExit();

                    if (proc.ExitCode == 0)
                        Log("Human Readable Map16 Conversion Success!", ConsoleColor.Green);
                    else
                    {
                        Log("Human Readable Map16 Conversion Failure!", ConsoleColor.Red);
                        return false;
                    }
                }
            }

            // export shared palette
            Log("Exporting Shared Palette...", ConsoleColor.Cyan);
            if (string.IsNullOrWhiteSpace(config.SharedPalettePath))
                Log("No path for Shared Palette provided!", ConsoleColor.Red);
            else if (string.IsNullOrWhiteSpace(config.LunarMagicPath))
                Log("No Lunar Magic Path provided!", ConsoleColor.Red);
            else if (!File.Exists(config.LunarMagicPath))
                Log("Could not find Lunar Magic at the provided path!", ConsoleColor.Red);
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                ProcessStartInfo psi = new ProcessStartInfo(config.LunarMagicPath,
                            $"-ExportSharedPalette \"{config.OutputPath}\" \"{config.SharedPalettePath}\"");
                var p = Process.Start(psi);
                p.WaitForExit();

                if (p.ExitCode == 0)
                    Log("Shared Palette Export Success!", ConsoleColor.Green);
                else
                {
                    Log("Shared Palette Export Failure!", ConsoleColor.Red);
                    return false;
                }
            }

            // export title moves
            Log("Exporting Title Moves...", ConsoleColor.Cyan);
            if (string.IsNullOrWhiteSpace(config.TitleMovesPath))
                Log("No path for Title Moves provided!", ConsoleColor.Red);
            else if (string.IsNullOrWhiteSpace(config.LunarMagicPath))
                Log("No Lunar Magic Path provided!", ConsoleColor.Red);
            else if (!File.Exists(config.LunarMagicPath))
                Log("Could not find Lunar Magic at the provided path!", ConsoleColor.Red);
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                ProcessStartInfo psi = new ProcessStartInfo(config.LunarMagicPath,
                            $"-ExportTitleMoves \"{config.OutputPath}\" \"{config.TitleMovesPath}\"");
                var p = Process.Start(psi);
                p.WaitForExit();

                if (p.ExitCode == 0)
                    Log("Title Moves Export Success!", ConsoleColor.Green);
                else
                {
                    Log("Title Moves Export Failure!", ConsoleColor.Red);
                    return false;
                }
            }

            // save levels
            if (!ExportLevels(config))
                return false;

            Console.WriteLine();
            return true;
        }

        static public bool CreatePatch(Config config, string cleanROM, string hackROM, string outputBPS)
        {
            Log($"Creating Patch {outputBPS}\n\twith {hackROM}\n\tover {cleanROM}", ConsoleColor.Yellow);

            Console.ForegroundColor = ConsoleColor.Yellow;
            var psi = new ProcessStartInfo(config.FlipsPath,
                    $"--create --bps-delta \"{cleanROM}\" \"{hackROM}\" \"{outputBPS}\"");
            var p = Process.Start(psi);
            p.WaitForExit();

            if (p.ExitCode == 0)
                return true;
            else
                return false;
        }

        static private bool ExportLevels(Config config)
        {
            Log("Exporting All Levels...", ConsoleColor.Cyan);
            if (string.IsNullOrWhiteSpace(config.LevelsPath))
                Log("No path for Levels provided!", ConsoleColor.Red);
            else if (string.IsNullOrWhiteSpace(config.LunarMagicPath))
                Log("No Lunar Magic Path provided!", ConsoleColor.Red);
            else if (!File.Exists(config.LunarMagicPath))
                Log("Could not find Lunar Magic at the provided path!", ConsoleColor.Red);
            else if (!File.Exists(config.OutputPath))
                Log("Output ROM does not exist! Build first!", ConsoleColor.Red);
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                ProcessStartInfo psi = new ProcessStartInfo(config.LunarMagicPath,
                            $"-ExportMultLevels \"{config.OutputPath}\" \"{config.LevelsPath}{Path.DirectorySeparatorChar}level\"");
                var p = Process.Start(psi);
                p.WaitForExit();

                if (p.ExitCode == 0)
                {
                    Log("Levels Export Success!", ConsoleColor.Green);
                    return true;
                }
                else
                {
                    Log("Levels Export Failure!", ConsoleColor.Red);
                }
            }

            return false;
        }

        internal static bool CreateVanillaGraphics(Config config)
        {
            return false;
            throw new NotImplementedException();
        }

        internal static bool CreateVanillaMap16(Config config)
        {
            return false;

            throw new NotImplementedException();
        }

        internal static bool CreateVanillaHumanReadableMap16(Config config)
        {
            return false;

            throw new NotImplementedException();
        }

        internal static bool CreateVanillaSharedPalettes(Config config)
        {
            return false;

            throw new NotImplementedException();
        }

        internal static bool CreateVanillaGlobalData(Config config)
        {
            return false;

            throw new NotImplementedException();
        }
    }
}
