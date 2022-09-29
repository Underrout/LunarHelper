using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LunarHelper
{
    enum InsertableType
    {
        Pixi,
        Amk,
        Gps,
        UberAsm,
        Graphics,
        Map16,
        SharedPalettes,
        GlobalData,
        TitleMoves,
        Levels,
        SinglePatch,  // a single patch, specified by name
        Patches  // refers to all not otherwise specified patches
    }

    class Insertable
    {
        public readonly InsertableType type;
        public readonly string normalized_relative_patch_path;

        public Insertable(InsertableType type, string user_specified_patch_path = null)
        {
            this.type = type;
            this.normalized_relative_patch_path = user_specified_patch_path.Replace('/', '\\')
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToLowerInvariant(); ;
        }
    }
}
