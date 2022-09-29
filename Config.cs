using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;

namespace LunarHelper
{
    public class Config
    {
        public string WorkingDirectory;

        public string OutputPath;
        public string TempPath;
        public string CleanPath;
        public string PackagePath;

        public string AsarPath;
        public string AsarOptions;

        public string UberASMPath;
        public string UberASMOptions;

        public string GPSPath;
        public string GPSOptions;

        public string PixiPath;
        public string PixiOptions;

        public string AddMusicKPath;
        public string AddmusicKOptions;

        public string LunarMagicPath;
        public string LunarMagicLevelImportFlags;

        public string FlipsPath;

        public string HumanReadableMap16CLI;
        public string HumanReadableMap16Directory;

        public string InitialPatch;
        public string LevelsPath;
        public string Map16Path;
        public string SharedPalettePath;
        public string GlobalDataPath;
        public string TitleMovesPath;

        public List<string> Patches = new List<string>();

        public string TestLevel;
        public string TestLevelDest;
        public string EmulatorPath;
        public string EmulatorOptions;

        public bool ReloadEmulatorAfterBuild;
        public bool SuppressArbitraryDepsWarning;

        #region load

        static public Config Load(out string error, Config config = null, string search_path_prefix = "")
        {
            error = "";

            try
            {
                var data = new List<string>();
                foreach (var file in Directory.EnumerateFiles(Directory.GetCurrentDirectory(), search_path_prefix + "config*.txt", SearchOption.TopDirectoryOnly))
                    data.Add(File.ReadAllText(file));
                return Load(data, config);
            }
            catch (Exception e)
            {
                error = e.Message;
                return null;
            }
        }

        static private Config Load(List<string> data, Config config)
        {
            if (config == null)
                config = new Config();

            var common_patches = new List<string>(config.Patches);

            Dictionary<string, string> vars = new Dictionary<string, string>();
            Dictionary<string, List<string>> lists = new Dictionary<string, List<string>>();
            List<Insertable> build_order = new List<Insertable>();
            List<(Insertable, List <Insertable>)> quick_build_triggers = new List<(Insertable, List<Insertable>)>();

            foreach (var d in data)
                Parse(d, vars, lists, build_order, quick_build_triggers);

            config.WorkingDirectory = vars.GetValueOrDefault("dir", config.WorkingDirectory);
            config.OutputPath = vars.GetValueOrDefault("output", config.OutputPath);
            config.TempPath = vars.GetValueOrDefault("temp", config.TempPath);
            config.CleanPath = vars.GetValueOrDefault("clean", config.CleanPath);
            config.PackagePath = vars.GetValueOrDefault("package", config.PackagePath);
            config.AsarPath = vars.GetValueOrDefault("asar_path", config.AsarPath);
            config.AsarOptions = vars.GetValueOrDefault("asar_options", config.AsarOptions);
            config.UberASMPath = vars.GetValueOrDefault("uberasm_path", config.UberASMPath);
            config.UberASMOptions = vars.GetValueOrDefault("uberasm_options", config.UberASMOptions);
            config.GPSPath = vars.GetValueOrDefault("gps_path", config.GPSPath);
            config.GPSOptions = vars.GetValueOrDefault("gps_options", config.GPSOptions);
            config.PixiPath = vars.GetValueOrDefault("pixi_path", config.PixiPath);
            config.PixiOptions = vars.GetValueOrDefault("pixi_options", config.PixiOptions);
            config.AddMusicKPath = vars.GetValueOrDefault("addmusick_path", config.AddMusicKPath);
            config.AddmusicKOptions = vars.GetValueOrDefault("addmusick_options", config.AddmusicKOptions);
            config.LunarMagicPath = vars.GetValueOrDefault("lm_path", config.LunarMagicPath);
            config.LunarMagicLevelImportFlags = vars.GetValueOrDefault("lm_level_import_flags", config.LunarMagicLevelImportFlags);
            config.FlipsPath = vars.GetValueOrDefault("flips_path", config.FlipsPath);
            config.HumanReadableMap16CLI = vars.GetValueOrDefault("human_readable_map16_cli_path", config.HumanReadableMap16CLI);
            config.HumanReadableMap16Directory = vars.GetValueOrDefault("human_readable_map16_directory_path", config.HumanReadableMap16Directory);
            config.LevelsPath = vars.GetValueOrDefault("levels", config.LevelsPath);
            config.Map16Path = vars.GetValueOrDefault("map16", config.Map16Path);
            config.SharedPalettePath = vars.GetValueOrDefault("shared_palette", config.SharedPalettePath);
            config.GlobalDataPath = vars.GetValueOrDefault("global_data", config.GlobalDataPath);
            config.TitleMovesPath = vars.GetValueOrDefault("title_moves", config.TitleMovesPath);
            config.InitialPatch = vars.GetValueOrDefault("initial_patch", config.InitialPatch);

            if (lists.ContainsKey("patches"))
            {
                config.Patches = common_patches.Concat(lists.GetValueOrDefault("patches")).ToList();
            }

            config.TestLevel = vars.GetValueOrDefault("test_level", config.TestLevel);
            config.TestLevelDest = vars.GetValueOrDefault("test_level_dest", config.TestLevelDest);
            config.EmulatorPath = vars.GetValueOrDefault("emulator_path", config.EmulatorPath);
            config.EmulatorOptions = vars.GetValueOrDefault("emulator_options", config.EmulatorOptions);

            if (vars.ContainsKey("reload_emulator_after_build"))
            {
                vars.TryGetValue("reload_emulator_after_build", out string reload_emulator);
                config.ReloadEmulatorAfterBuild = reload_emulator != null ? (new[] { "yes", "true" }).AsSpan().Contains(reload_emulator.Trim()) : false;
            }

            if (vars.ContainsKey("suppress_arbitrary_dependency_warning"))
            {
                vars.TryGetValue("suppress_arbitrary_dependency_warning", out string suppress_arbitrary_deps_warning);
                config.SuppressArbitraryDepsWarning = suppress_arbitrary_deps_warning != null ? (new[] { "yes", "true" }).AsSpan().Contains(suppress_arbitrary_deps_warning.Trim()) : false;
            }

            if (build_order.Count() == 0)
            {
                throw new Exception("'build_order' list must be specified");
            }

            return config;
        }

        static private void Parse(string data, Dictionary<string, string> vars, Dictionary<string, 
            List<string>> lists, List<Insertable> build_order, List<(Insertable, List<Insertable>)> quick_build_triggers)
        {
            var lines = data.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string str = lines[i];
                string peek = null;
                if (i < lines.Length - 1)
                    peek = lines[i + 1];

                if (str.StartsWith("--"))
                {
                    // comment
                }
                else if (str.Contains('='))
                {
                    // var
                    var sp = str.Split('=', 2);
                    // if (sp.Length != 2)
                    //    throw new Exception("Malformed assignment");

                    var key = sp[0].Trim();
                    if (vars.ContainsKey(key))
                        throw new Exception($"Duplicate config key: '{key}'");
                    vars.Add(key, sp[1].Trim());
                }
                else if (peek != null && peek.Trim() == "[")
                {
                    if (new[] {"quick_build_triggers", "build_order"}.Contains(str))
                    {
                        ParseDependencyList(str, build_order, quick_build_triggers, lines, i + 2);
                        continue;
                    }
                    // list
                    var list = new List<string>();
                    lists.Add(str.Trim(), list);
                    i += 2;

                    while (true)
                    {
                        if (i >= lines.Length)
                            throw new Exception("Malformed list");

                        str = lines[i];
                        if (str.Trim() == "]")
                            break;
                        else
                            list.Add(str.Trim().Replace("\\", "/"));

                        i++;
                    }
                }
            }
        }

        static private void ParseDependencyList(string list_name, List<Insertable> build_order, 
            List<(Insertable, List<Insertable>)> quick_build_triggers, string[] lines, int line_idx)
        {
            while (true)
            {
                if (line_idx >= lines.Length)
                    throw new Exception("Malformed list");

                string str = lines[line_idx];
                if (str.Trim() == "]")
                    break;
                else
                {
                    if (list_name == "build_order")
                    {
                        build_order.Append(ParseInsertable(str));
                    }
                    else
                    {
                        (var trigger, var insertables) = ParseQuickBuildTriggers(str);
                        quick_build_triggers.Add((trigger, insertables));
                    }
                }

                line_idx++;
            }
        }

        static private (Insertable, List<Insertable>) ParseQuickBuildTriggers(string line)
        {
            string trimmed_line = line.Trim();

            int arrow_idx = trimmed_line.IndexOf(" -> ");

            if (arrow_idx == -1)
            {
                throw new Exception($"Malformed quick build trigger, missing '->': '{line}'");
            }

            Insertable trigger = ParseInsertable(trimmed_line.Substring(0, arrow_idx).Trim());

            var to_insert = trimmed_line.Substring(arrow_idx + 4).Split(", ")
                .Select(i => ParseInsertable(i.Trim()));

            return (trigger, to_insert.ToList());
        }

        static private Insertable ParseInsertable(string insertable_string)
        {
            string trimmed = insertable_string.Trim();

            InsertableType insertable_type;
            bool success = Enum.TryParse(trimmed, true, out insertable_type);

            if (success && insertable_type != InsertableType.SinglePatch)
            {
                return new Insertable(insertable_type);
            }
            else
            {
                return new Insertable(InsertableType.SinglePatch, trimmed);
            }
        }

        #endregion
    }
}
