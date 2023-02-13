using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using AsarCLR;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Reflection;
using QuikGraph;
using QuikGraph.Algorithms.ConnectedComponents;

namespace LunarHelper
{
    using static Program;

    internal class Importer
    {
        static private string UBERASM_SUCCESS_STRING = "Codes inserted successfully.";

        static private string LH_VERSION_DEFINE_NAME = "LH_VERSION";
        static private string LH_ASSEMBLING_DEFINE_NAME = "LH_ASSEMBLING";
        static private string LH_GLOBULE_FOLDER_DEFINE_NAME = "LH_GLOBULES_FOLDER";

        static private Dictionary<string, string> GetStandardDefineDict(string output_folder)
        {
            var ver = Assembly.GetExecutingAssembly().GetName().Version;

            var globule_folder = Path.Combine(Path.GetFullPath(output_folder), ".lunar_helper/globules")
                .Replace('\\', '/');

            return new Dictionary<string, string>
            {
                { LH_VERSION_DEFINE_NAME, ver.ToString().Substring(0, 5) },
                { LH_VERSION_DEFINE_NAME + "_MAJOR", ver.Major.ToString() },
                { LH_VERSION_DEFINE_NAME + "_MINOR", ver.Minor.ToString() },
                { LH_VERSION_DEFINE_NAME + "_PATCH", ver.Build.ToString() },
                { LH_ASSEMBLING_DEFINE_NAME, "1" },
                { LH_GLOBULE_FOLDER_DEFINE_NAME, globule_folder }
            };
        }

        static public void CreateStandardDefines(string output_folder)
        {
            Directory.CreateDirectory(Path.Combine(output_folder, ".lunar_helper"));

            var ver = Assembly.GetExecutingAssembly().GetName().Version;

            var globule_folder = Path.Combine(Path.GetFullPath(output_folder), ".lunar_helper/globules/")
                .Replace('\\', '/');

            File.WriteAllText(Path.Combine(output_folder, ".lunar_helper/defines.asm"),
                "includeonce\n\n" +
                "; Asar compatible file containing information about Lunar Helper, feel free to inscsrc this if needed,\n" +
                "; it is recreated before every (Quick) Build\n\n" +
                "; Define containing Lunar Helper's version number as a string\n" +
                $"!{LH_VERSION_DEFINE_NAME} = \"{ver.ToString().Substring(0, 5)}\"\n\n" +
                "; Defines containing Lunar Helper's version number as individual numbers\n" +
                "; For example, if assembled with Lunar Helper 2.6.0, MAJOR will be 2, MINOR will be 6 and PATCH will be 0\n" +
                $"!{LH_VERSION_DEFINE_NAME}_MAJOR = {ver.Major}\n" +
                $"!{LH_VERSION_DEFINE_NAME}_MINOR = {ver.Minor}\n" +
                $"!{LH_VERSION_DEFINE_NAME}_PATCH = {ver.Build}\n\n" +
                "; Define that just serves as a marker so you can check if a file is being assembled using Lunar Helper if needed\n" +
                $"!{LH_ASSEMBLING_DEFINE_NAME} = 1\n\n" +
                $"; Define that contains the path to Lunar Helper's globule folder, useful for calling globules without having to " +
                $"hardcode a user-specific path to the globule\n" +
                $"!{LH_GLOBULE_FOLDER_DEFINE_NAME} = {globule_folder}\n"
            );
        }

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

            if (!config.InvokedOnCommandLine && !File.Exists(config.GlobalDataPath))
            {
                var res = Prompt($"'{config.GlobalDataPath}' file not found. " +
                $"Export and use vanilla global data?");

                if (res)
                {
                    if (!Exporter.CreateVanillaGlobalData(config))
                        return false;
                }
            }

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

            // supress message box prompts if invoked on CLI
            var supress_prompts = config.InvokedOnCommandLine ? "-NoPrompts" : "";

            //Overworld
            {

                Console.ForegroundColor = ConsoleColor.Yellow;
                psi = new ProcessStartInfo(config.LunarMonitorLoaderPath,
                            $"{config.LunarMonitorLoaderOptions} {supress_prompts} " +
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
                psi = new ProcessStartInfo(config.LunarMonitorLoaderPath,
                            $"{config.LunarMonitorLoaderOptions} {supress_prompts} " +
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
                psi = new ProcessStartInfo(config.LunarMonitorLoaderPath,
                            $"{config.LunarMonitorLoaderOptions} {supress_prompts} " +
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
                psi = new ProcessStartInfo(config.LunarMonitorLoaderPath,
                            $"{config.LunarMonitorLoaderOptions} {supress_prompts} " +
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

            if (!config.InvokedOnCommandLine && !File.Exists(config.SharedPalettePath))
            {
                var res = Prompt($"'{config.SharedPalettePath}' file not found. " +
                $"Export and use vanilla shared palettes?");

                if (res)
                {
                    if (!Exporter.CreateVanillaSharedPalettes(config))
                        return false;
                }
            }

            // supress message box prompts if invoked on CLI
            var supress_prompts = config.InvokedOnCommandLine ? "-NoPrompts" : "";

            Console.ForegroundColor = ConsoleColor.Yellow;
            ProcessStartInfo psi = new ProcessStartInfo(config.LunarMonitorLoaderPath,
                        $"{config.LunarMonitorLoaderOptions} {supress_prompts} " +
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

            // supress message box prompts if invoked on CLI
            var supress_prompts = config.InvokedOnCommandLine ? "-NoPrompts" : "";

            Console.ForegroundColor = ConsoleColor.Yellow;
            ProcessStartInfo psi = new ProcessStartInfo(config.LunarMonitorLoaderPath,
                        $"{config.LunarMonitorLoaderOptions} {supress_prompts}" +
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

                if (!config.InvokedOnCommandLine && !Directory.Exists(humanReadableDirectory))
                {
                    var res = Prompt($"'{humanReadableDirectory}' folder is missing or empty. " +
                    $"Export and use vanilla map16?");

                    if (res)
                    {
                        if (!Exporter.CreateVanillaMap16(config))
                            return false;
                    }
                }

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

            if (!config.InvokedOnCommandLine && !File.Exists(config.Map16Path))
            {
                var res = Prompt($"'{config.Map16Path}' file not found. " +
                $"Export and use vanilla map16?");

                if (res)
                {
                    if (!Exporter.CreateVanillaMap16(config))
                        return false;
                }
            }

            // supress message box prompts if invoked on CLI
            var supress_prompts = config.InvokedOnCommandLine ? "-NoPrompts" : "";

            Console.ForegroundColor = ConsoleColor.Yellow;
            ProcessStartInfo psi = new ProcessStartInfo(config.LunarMonitorLoaderPath,
                        $"{config.LunarMonitorLoaderOptions} {supress_prompts} " +
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
            psi.UseShellExecute = false;
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
                Log("UberASM Success!\n", ConsoleColor.Green);
            else
            {
                Log("UberASM Failure!\n", ConsoleColor.Red);
                return false;
            }

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
                Log("Pixi Success!\n", ConsoleColor.Green);
            else
            {
                Log("Pixi Failure!\n", ConsoleColor.Red);
                return false;
            }

            return true;
        }

        static private bool Prompt(string prompt)
        {
            ConsoleKey res;
            do
            {
                Log($"{prompt} Y/N", ConsoleColor.White);

                res = Console.ReadKey(true).Key;
            } while (!(new[] { ConsoleKey.N, ConsoleKey.Y }).Contains(res));

            return res == ConsoleKey.Y;
        }

        static public bool ImportGraphics(Config config)
        {
            Log("Graphics", ConsoleColor.Cyan);
            {
                var graphics_folder = Path.Join(Path.GetDirectoryName(config.OutputPath), "Graphics");
                var graphics_folder_missing_or_empty = !Directory.Exists(graphics_folder) ||
                    !Directory.EnumerateFiles(graphics_folder).Any();

                if (!config.InvokedOnCommandLine && graphics_folder_missing_or_empty)
                {
                    var res = Prompt($"'{graphics_folder}' folder is missing or empty. " +
                    $"Export and use vanilla graphics?");

                    if (res)
                    {
                        if (!Exporter.CreateVanillaGraphics(config))
                            return false;
                    }
                }

                var output_gfx = Path.Combine(Path.GetDirectoryName(config.OutputPath), "Graphics");
                var temp_gfx = Path.Combine(Path.GetDirectoryName(config.TempPath), "Graphics");

                try
                {
                    Microsoft.VisualBasic.FileIO.FileSystem.CopyDirectory(
                        output_gfx,
                        temp_gfx
                    );
                }
                catch (Exception)
                {
                    // pass
                }

                // supress message box prompts if invoked on CLI
                var supress_prompts = config.InvokedOnCommandLine ? "-NoPrompts" : "";

                Console.ForegroundColor = ConsoleColor.Yellow;
                ProcessStartInfo psi = new ProcessStartInfo(config.LunarMonitorLoaderPath,
                            $"{config.LunarMonitorLoaderOptions} {supress_prompts} " +
                            $"-ImportGFX \"{Path.GetFileName(config.TempPath)}\"");

                psi.WorkingDirectory = Path.GetDirectoryName(config.TempPath);

                var p = Process.Start(psi);
                p.WaitForExit();

                try
                {
                    // delete temporary graphics folder, but only if it's not identical to the output 
                    // graphics folder
                    if (new Uri(Path.GetFullPath(Path.GetDirectoryName(config.OutputPath))).LocalPath !=
                        new Uri(Path.GetFullPath(Path.GetDirectoryName(config.TempPath))).LocalPath)
                        Directory.Delete(temp_gfx, true);
                }
                catch
                {
                    // pass
                }

                if (p.ExitCode == 0)
                    Log("Import Graphics Success!\n", ConsoleColor.Green);
                else
                {
                    Log("Import Graphics Failure!\n", ConsoleColor.Red);
                    return false;
                }

                return true;
            }
        }

        static public List<string> DetermineGlobuleInsertionOrder(DependencyGraph graph)
        {
            BidirectionalGraph<string, Edge<string>> globule_import_graph = new BidirectionalGraph<string, Edge<string>>();

            foreach (var globule_root in graph.globule_roots)
            {
                foreach (var out_edge in graph.dependency_graph.OutEdges(globule_root))
                {
                    if (out_edge.Target is GlobuleRootVertex)
                    {
                        var importer_name = ((GlobuleRootVertex)globule_root).globule_name;
                        var imported_name = ((GlobuleRootVertex)out_edge.Target).globule_name;
                        globule_import_graph.AddVertex(importer_name);
                        globule_import_graph.AddVertex(imported_name);
                        globule_import_graph.AddEdge(new Edge<string>(imported_name, importer_name));
                    }
                }
            }

            if (globule_import_graph.Edges.Any(e => e.Source.Equals(e.Target)))
                throw new Exception("Cyclic imports detected in globules, cannot insert globules");

            var ssc = new StronglyConnectedComponentsAlgorithm<string, Edge<string>>(globule_import_graph);

            ssc.Compute();

            bool cycles_present = ssc.ComponentCount != globule_import_graph.VertexCount;  // if all strongly connected components are
                                                                                           // single vertices, our graph is acyclic

            if (cycles_present)
                throw new Exception("Cyclic imports detected in globules, cannot insert globules");

            List<string> order = new List<string>();
            while (globule_import_graph.VertexCount != 0)
            {
                var source = globule_import_graph.Vertices.First(v => globule_import_graph.InDegree(v) == 0);
                order.Add(source);
                globule_import_graph.RemoveVertex(source);
            }

            foreach (var globule_root in graph.globule_roots)
            {
                var name = ((GlobuleRootVertex)globule_root).globule_name;
                if (!order.Contains(name))
                {
                    order.Add(name);
                }
            }

            return order;
        }

        static public bool ImportSingleLevel(Config config, string level_path)
        {
            // supress message box prompts if invoked on CLI
            var supress_prompts = config.InvokedOnCommandLine ? "-NoPrompts" : "";

            Console.ForegroundColor = ConsoleColor.Yellow;
            ProcessStartInfo psi = new ProcessStartInfo(config.LunarMonitorLoaderPath,
                        $"{config.LunarMonitorLoaderOptions} {supress_prompts} " +
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

            var output_exgfx = Path.Combine(Path.GetDirectoryName(config.OutputPath), "ExGraphics");
            var temp_exgfx = Path.Combine(Path.GetDirectoryName(config.TempPath), "ExGraphics");

            try
            {
                Microsoft.VisualBasic.FileIO.FileSystem.CopyDirectory(
                    output_exgfx,
                    temp_exgfx
                );
            }
            catch
            {
                // pass
            }

            // supress message box prompts if invoked on CLI
            var supress_prompts = config.InvokedOnCommandLine ? "-NoPrompts" : "";

            Console.ForegroundColor = ConsoleColor.Yellow;
            ProcessStartInfo psi = new ProcessStartInfo(config.LunarMonitorLoaderPath,
                        $"{config.LunarMonitorLoaderOptions} {supress_prompts} " +
                        $"-ImportExGFX \"{Path.GetFileName(config.TempPath)}\"");

            psi.WorkingDirectory = Path.GetDirectoryName(config.TempPath);

            var p = Process.Start(psi);
            p.WaitForExit();

            try
            {
                // delete temporary exgraphics folder, but only if it's not identical to the output 
                // exgraphics folder
                if (new Uri(Path.GetFullPath(Path.GetDirectoryName(config.OutputPath))).LocalPath !=
                    new Uri(Path.GetFullPath(Path.GetDirectoryName(config.TempPath))).LocalPath)
                    Directory.Delete(temp_exgfx, true);
            }
            catch
            {
                // pass
            }

            if (p.ExitCode == 0)
                Log("Import ExGraphics Success!\n", ConsoleColor.Green);
            else
            {
                Log("Import ExGraphics Failure!\n", ConsoleColor.Red);
                return false;
            }

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

        private static void WriteGlobuleImprint(string output_directory, string globule_name, Asarlabel[] labels, HashSet<string> imported_names)
        {
            var globules_folder = Path.Combine(output_directory, ".lunar_helper/globules");

            if (!Directory.Exists(globules_folder))
                Directory.CreateDirectory(globules_folder);

            var imprint_path = Path.Combine(globules_folder, globule_name);

            var globule_stream = new StreamWriter(imprint_path, false);

            globule_stream.WriteLine("incsrc \"../call_globule.asm\"\n");

            foreach (var label in labels)
            {
                if (label.Name.StartsWith(':'))
                    continue;

                if (label.Name.Contains('_') && imported_names.Contains(label.Name.Substring(0, label.Name.IndexOf('_'))))
                    continue;  // skip imported labels

                var prefixed_label = Path.GetFileNameWithoutExtension(globule_name).Replace(' ', '_') + '_' + label.Name;
                var prefixed_label_as_define = '!' + prefixed_label;
                var location = Convert.ToString(label.Location, 16).ToUpper();

                globule_stream.WriteLine($"{prefixed_label} = ${location}");
                globule_stream.WriteLine($"{prefixed_label_as_define} = ${location}");
            }

            globule_stream.Close();
        }

        public static void FinalizeGlobuleImprints(string output_directory)
        {
            var inserted_globules_folder = Path.Combine(output_directory, ".lunar_helper/inserted_globules");
            var globules_folder = Path.Combine(output_directory, ".lunar_helper/globules");

            if (Directory.Exists(inserted_globules_folder))
                Directory.Delete(inserted_globules_folder, true);

            Directory.CreateDirectory(globules_folder);

            Microsoft.VisualBasic.FileIO.FileSystem.CopyDirectory(globules_folder, inserted_globules_folder);
        }

        public static void WriteCallGlobuleMacroFile(string output_directory)
        {
            File.WriteAllText(Path.Combine(output_directory, ".lunar_helper/call_globule.asm"),
                "includeonce\n\nmacro call_globule(globule_label)\n\tPHB\n\t" +
                "LDA.b #<globule_label>>>16\n\tPHA\n\tPLB\n\tJSL <globule_label>\n\tPLB\nendmacro\n"
            );
        }

        public static void CopyGlobuleImprints(string output_directory, string[] globule_names_to_copy)
        {
            var base_path = Path.Combine(output_directory, ".lunar_helper");

            foreach (string globule_name in globule_names_to_copy)
            {
                File.Copy(Path.Combine(base_path, $"inserted_globules/{globule_name}.asm"), 
                    Path.Combine(base_path, $"globules/{globule_name}.asm"), true);
            }
        }

        public static void ClearGlobuleFolder(string output_directory)
        {
            var globules_folder = Path.Combine(output_directory, ".lunar_helper/globules");

            if (Directory.Exists(globules_folder))
                Directory.Delete(globules_folder, true);

            Directory.CreateDirectory(globules_folder);
        }

        public static bool CleanGlobules(string output_directory, string temp_rom_path, string[] globule_names_to_clean)
        {
            var clean_patch_path = Path.GetTempFileName();
            var clean_patch_stream = new StreamWriter(clean_patch_path);

            var inserted_globules_folder = Path.Combine(output_directory, ".lunar_helper/inserted_globules");
            var globules_folder = Path.Combine(output_directory, ".lunar_helper/globules");

            foreach (var globule_name in globule_names_to_clean)
            {
                var globule_path = Path.Combine(inserted_globules_folder, $"{globule_name}.asm");
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

                var prev_imprint = Path.Combine(globules_folder, $"{globule_name}.asm");

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

        public static bool ApplyAllGlobules(string output_folder, string temp_rom_path, string globules_folder, DependencyGraph graph)
        {
            var order = DetermineGlobuleInsertionOrder(graph);

            foreach (var globule_name in order)
            {
                var full_globule_path = Path.Join(globules_folder, globule_name) + ".asm";

                if (!ApplyGlobule(output_folder, temp_rom_path, full_globule_path, graph))
                {
                    return false;
                }
            }

            return true;
        }

        public static bool ApplyGlobule(string output_folder, string temp_rom_path, string globule_path, DependencyGraph graph)
        {
            Log($"Globule '{globule_path}'", ConsoleColor.Cyan);

            var imports = new List<string>();
            var import_names = new HashSet<string>();
            foreach (var out_edge in graph.dependency_graph.OutEdges(graph.GetOrCreateFileNameVertex(globule_path)))
            {
                if (out_edge.Target is GlobuleRootVertex)
                {
                    var import_name = ((GlobuleRootVertex)out_edge.Target).globule_name;
                    imports.Add(Path.Combine(output_folder, $".lunar_helper/globules/{import_name}.asm"));
                    import_names.Add(import_name);
                }
            }

            var temp_patch_path = Path.GetTempFileName();

            var temp_patch_stream = new StreamWriter(temp_patch_path);
            temp_patch_stream.WriteLine("warnings disable W1011");  // any freespace used in the globule is cleaned by LH anyway, no need to warn about "leaks"
            temp_patch_stream.WriteLine("if read1($00ffd5) == $23\nsa1rom\nendif");
            temp_patch_stream.WriteLine("freecode cleaned");
            foreach (var import_path in imports)
                temp_patch_stream.WriteLine($"incsrc \"{Path.GetFullPath(import_path).Replace('\\', '/')}\"");
            temp_patch_stream.WriteLine($"incsrc \"{Path.GetFullPath(globule_path).Replace('\\', '/')}\"");
            temp_patch_stream.Close();

            (var rom, var header) = GetRomHeaderAndData(temp_rom_path);
            var define_dict = GetStandardDefineDict(output_folder);
            var res = Asar.patch(temp_patch_path, ref rom, null, true, define_dict);

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

            WriteGlobuleImprint(output_folder, Path.GetFileName(globule_path), Asar.getlabels(), import_names);

            Log($"Globule Insertion Success!\n", ConsoleColor.Green);

            return true;
        }

        public static bool ApplyAsarPatch(Config config, string patch_path)
        {
            Log($"Patch '{patch_path}'", ConsoleColor.Cyan);

            (var rom, var header) = GetRomHeaderAndData(config.TempPath);

            var define_dict = GetStandardDefineDict(Path.GetDirectoryName(Path.GetFullPath(config.OutputPath)));
            var res = Asar.patch(Path.GetFullPath(patch_path), ref rom, null, true, define_dict);

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

            if (!config.InvokedOnCommandLine && !Directory.Exists(config.LevelsPath))
            {
                var res = Prompt($"'{config.LevelsPath}' folder not found. Create it?");

                if (res)
                    Directory.CreateDirectory(config.LevelsPath);
            }

            if (Directory.Exists(config.LevelsPath) && !Directory.EnumerateFiles(config.LevelsPath, "*.mwl", SearchOption.TopDirectoryOnly).Any())
            {
                Log("No levels to be inserted yet.\n", ConsoleColor.Yellow);
                return true;
            }

            // supress message box prompts if invoked on CLI
            var supress_prompts = config.InvokedOnCommandLine ? "-NoPrompts" : "";

            Console.ForegroundColor = ConsoleColor.Yellow;
            ProcessStartInfo psi = new ProcessStartInfo(config.LunarMonitorLoaderPath,
                        $"{config.LunarMonitorLoaderOptions} {supress_prompts} " +
                        $"-ImportMultLevels \"{config.TempPath}\" " +
                        $"\"{config.LevelsPath}\" {config.LunarMagicLevelImportFlags ?? ""}");
            var p = Process.Start(psi);
            p.WaitForExit();

            if (p.ExitCode == 0)
            {
                Log("Levels Import Success!\n", ConsoleColor.Green);
                return true;
            }
            else
            {
                Log("Levels Import Failure!\n", ConsoleColor.Red);
                return false;
            }
        }
    }
}
