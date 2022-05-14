using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Text.Json;
using System.Linq;

using SMWPatcher;

namespace LunarHelper
{
    class BuildPlan
    {
        public bool uptodate { get; set; } = true;
        public bool rebuild { get; set; } = false;
        public bool apply_gps { get; set; } = false;
        public bool apply_pixi { get; set; } = false;
        public bool may_need_two_pixi_passes { get; set; } = false;
        public bool apply_uberasm { get; set; } = false;
        public bool apply_addmusick { get; set; } = false;
        public bool insert_map16 { get; set; } = false;
        public bool insert_gfx { get; set; } = false;
        public bool insert_exgfx { get; set; } = false;
        public bool insert_shared_palettes { get; set; } = false;
        public bool insert_global_patch { get; set; } = false;
        public bool insert_title_moves { get; set; } = false;
        public IList<string> patches_to_apply { get; set; } = new List<string>();
        public IList<string> levels_to_insert { get; set; } = new List<string>();
        public bool insert_all_levels { get; set; } = false;

        public static BuildPlan PlanBuild(Config config)
        {
            var plan = new BuildPlan();

            // check whether we need to rebuild completely

            Program.Log("Analyzing previous build result", ConsoleColor.Cyan);

            if (!File.Exists(config.OutputPath))
            {
                Program.Log("No previously built ROM found, rebuilding ROM...", ConsoleColor.Yellow);
                plan.rebuild = true;
                plan.uptodate = false;
                return plan;
            }

            Program.Log($"Previously built ROM found at '{config.OutputPath}'!", ConsoleColor.Green);

            if (!File.Exists(".lunar_helper\\build_report.json"))
            {
                Program.Log("No previous build report found, rebuilding ROM...", ConsoleColor.Yellow);
                Console.WriteLine();
                plan.rebuild = true;
                plan.uptodate = false;
                return plan;
            }

            var jsonString = File.ReadAllText(".lunar_helper\\build_report.json");
            Report report;
            try
            {
                report = JsonSerializer.Deserialize<Report>(jsonString);
            }
            catch(Exception)
            {
                Program.Log("Previous build report was found but corrupted, rebuilding ROM...", ConsoleColor.Yellow);
                Console.WriteLine();
                plan.rebuild = true;
                plan.uptodate = false;
                return plan;
            }

            Dictionary<string, string> oldPatches = report.patches;
            Dictionary<string, string> newPatches = Program.GetPatchReport();

            if (report.patches != null)
            {
                var oldPatchPaths = new HashSet<string>(report.patches.Keys);
                var newPatchPaths = config.Patches == null ? null : new HashSet<string>(config.Patches);

                if (newPatches == null || !oldPatchPaths.IsSubsetOf(newPatchPaths))
                {
                    Program.Log("Previously built ROM contains patches that need to be removed, rebuilding ROM...", ConsoleColor.Yellow);
                    Console.WriteLine();
                    plan.rebuild = true;
                    plan.uptodate = false;
                    return plan;
                }
            }

            Dictionary<string, string> oldLevels = report.levels;
            Dictionary<string, string> newLevels = Program.GetLevelReport();

            if (report.levels != null)
            {
                var oldLevelPaths = new HashSet<string>(report.levels.Keys);
                var newLevelPaths = (config.LevelsPath == null || !Directory.Exists(config.LevelsPath)) ? null : 
                    new HashSet<string>(Directory.GetFiles(config.LevelsPath, "*.mwl", SearchOption.TopDirectoryOnly).Select(levelPath => levelPath.Replace("\\", "/")));

                if (newLevelPaths == null || !oldLevelPaths.IsSubsetOf(newLevelPaths))
                {
                    Program.Log("Previously built ROM contains levels that need to be removed, rebuilding ROM...", ConsoleColor.Yellow);
                    Console.WriteLine();
                    plan.rebuild = true;
                    plan.uptodate = false;
                    return plan;
                }
            }

            if (Report.HashFile(config.InitialPatch) != report.init_bps)
            {
                Program.Log("Change in initial patch detected, rebuilding ROM...", ConsoleColor.Yellow);
                Console.WriteLine();
                plan.rebuild = true;
                plan.uptodate = false;
                return plan;
            }

            Console.WriteLine();
            Program.Log("Attempting to reuse previously built ROM...", ConsoleColor.Cyan);

            // check if tools/patches need to be reapplied

            if (Report.HashFolder(config.SharedFolder) != report.shared_folders)
            {
                Program.Log("Change in shared folder detected, will reapply all tools and patches...", ConsoleColor.Yellow);
                plan.apply_addmusick = true;
                plan.apply_gps = true;
                plan.apply_pixi = true;
                plan.apply_uberasm = true;
                plan.uptodate = false;
                plan.patches_to_apply = new List<string>(newPatches.Keys);
            }
            else
            {
                if (Report.HashFolder(Path.GetDirectoryName(config.GPSPath)) != report.gps_folders || report.gps_options != config.GPSOptions)
                {
                    Program.Log("Change in GPS detected, will reapply GPS...", ConsoleColor.Yellow);
                    Console.WriteLine();
                    plan.apply_gps = true;
                    plan.uptodate = false;
                }

                if (Report.HashFolder(Path.GetDirectoryName(config.PixiPath)) != report.pixi_folders || report.pixi_options != config.PixiOptions)
                {
                    Program.Log("Change in PIXI detected, will reapply PIXI...", ConsoleColor.Yellow);
                    Console.WriteLine();
                    plan.apply_pixi = true;
                    plan.uptodate = false;

                    if (report.pixi_folders == null)
                    {
                        // PIXI was never applied to the ROM before, may need two passes if we're on LM 3.31
                        plan.may_need_two_pixi_passes = true;
                    }
                }

                // check which patches to apply

                if (report.asar_options == config.AsarOptions)
                {
                    foreach (var (patch, hash) in newPatches)
                    {
                        if (!oldPatches.ContainsKey(patch))
                        {
                            Program.Log($"New patch '{patch}' detected, will be inserted...", ConsoleColor.Yellow);
                            Console.WriteLine();
                            plan.patches_to_apply.Add(patch);
                            plan.uptodate = false;
                        }
                        else if (oldPatches[patch] != hash)
                        {
                            Program.Log($"Change in patch '{patch}' detected, will be reinserted...", ConsoleColor.Yellow);
                            Console.WriteLine();
                            plan.patches_to_apply.Add(patch);
                            plan.uptodate = false;
                        }
                    }
                }
                else
                {
                    Program.Log("Change in asar options detected, reapplying all patches...", ConsoleColor.Yellow);
                    Console.WriteLine();
                    plan.patches_to_apply = new List<string>(newPatches.Keys);
                    plan.uptodate = false;
                }

                if (Report.HashFolder(Path.GetDirectoryName(config.UberASMPath)) != report.uberasm_folders)
                {
                    Program.Log("Change in UberASMTool folder detected, will reapply UberASMTool...", ConsoleColor.Yellow);
                    Console.WriteLine();
                    plan.apply_uberasm = true;
                    plan.uptodate = false;
                }

                if (Report.HashFolder(Path.GetDirectoryName(config.AddMusicKPath)) != report.addmusick_folders || report.addmusick_options != config.AddmusicKOptions)
                {
                    Program.Log("Change in AddMusicK detected, will reapply AddMusicK...", ConsoleColor.Yellow);
                    Console.WriteLine();
                    plan.apply_addmusick = true;
                    plan.uptodate = false;
                }
            }

            // check resources

            // graphics

            // gfx

            if (Report.HashFolder("Graphics") != report.graphics)
            {
                Program.Log("Change in GFX detected, will insert GFX...", ConsoleColor.Yellow);
                Console.WriteLine();
                plan.insert_gfx = true;
                plan.uptodate = false;
            }

            // exgfx

            if (Report.HashFolder("ExGraphics") != report.exgraphics)
            {
                Program.Log("Change in ExGFX detected, will insert ExGFX...", ConsoleColor.Yellow);
                Console.WriteLine();
                plan.insert_exgfx = true;
                plan.uptodate = false;
            }

            // map16

            string map16hash;
            if (config.HumanReadableMap16CLI == null)
            {
                map16hash = Report.HashFile(config.Map16Path);
            }
            else
            {
                if (config.HumanReadableMap16Directory == null)
                {
                    map16hash = Report.HashFolder(Path.Combine(
                        Path.GetDirectoryName(config.Map16Path), Path.GetFileNameWithoutExtension(config.Map16Path)
                    ));
                }
                else
                {
                    map16hash = Report.HashFolder(config.HumanReadableMap16Directory);
                }
            }

            if (map16hash != report.map16)
            {
                Program.Log("Change in map16 detected, will insert map16...", ConsoleColor.Yellow);
                Console.WriteLine();
                plan.insert_map16 = true;
                plan.uptodate = false;
            }

            // title moves

            if (Report.HashFile(config.TitleMovesPath) != report.title_moves)
            {
                Program.Log("Change in title moves detected, will insert title moves...", ConsoleColor.Yellow);
                Console.WriteLine();
                plan.insert_title_moves = true;
                plan.uptodate = false;
            }

            // shared palettes

            if (Report.HashFile(config.SharedPalettePath) != report.shared_palettes)
            {
                Program.Log("Change in shared palettes detected, will insert shared palettes...", ConsoleColor.Yellow);
                Console.WriteLine();
                plan.insert_shared_palettes = true;
                plan.uptodate = false;
            }

            // global data

            if (Report.HashFile(config.GlobalDataPath) != report.global_data)
            {
                Program.Log("Change in global data detected, will insert global data...", ConsoleColor.Yellow);
                Console.WriteLine();
                plan.insert_global_patch = true;
                plan.uptodate = false;
            }

            // check which levels to insert

            if (report.lunar_magic_level_import_flags == config.LunarMagicLevelImportFlags)
            {
                foreach (var (level, hash) in newLevels)
                {
                    if (!oldLevels.ContainsKey(level))
                    {
                        Program.Log($"New level '{level}' detected, will be inserted...", ConsoleColor.Yellow);
                        Console.WriteLine();
                        plan.levels_to_insert.Add(level);
                        plan.uptodate = false;
                    }
                    else if (oldLevels[level] != hash)
                    {
                        Program.Log($"Changed level '{level}' detected, will be reinserted...", ConsoleColor.Yellow);
                        Console.WriteLine();
                        plan.levels_to_insert.Add(level);
                        plan.uptodate = false;
                    }
                }
            }
            else
            {
                Program.Log("Change in Lunar Magic level import flags detected, reinserting all levels...", ConsoleColor.Yellow);
                Console.WriteLine();
                plan.insert_all_levels = true;
                plan.uptodate = false;
            }

            // log whether already up to date

            if (plan.uptodate)
            {
                Program.Log("Previously built ROM should already be up to date!", ConsoleColor.Green);
                Console.WriteLine();
            }

            return plan;
        }
    }
}
