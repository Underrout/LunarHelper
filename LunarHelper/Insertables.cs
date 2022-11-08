using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LunarHelper
{
    public enum InsertableType
    {
        Pixi,
        AddMusicK,
        Gps,
        UberAsm,
        Graphics,
        ExGraphics,
        Map16,
        SharedPalettes,
        GlobalData,
        TitleMoves,
        SingleLevel,  // a single level
        Levels,  // all levels
        SinglePatch,  // a single patch, specified by name
        Patches  // refers to all not otherwise specified patches
    }

    public class Insertable
    {
        public readonly InsertableType type;
        public readonly string normalized_relative_path;

        public Insertable(InsertableType type, string path = null)
        {
            this.type = type;
            if (path != null)
                this.normalized_relative_path = path.Trim().Replace('\\', '/')
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToLowerInvariant();
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }
            else
            {
                if (obj is Insertable)
                {
                    Insertable other = (Insertable)obj;
                    if (this.type == other.type)
                    {
                        return (this.type != InsertableType.SinglePatch && this.type != InsertableType.SingleLevel) ||
                            this.normalized_relative_path == other.normalized_relative_path;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(type, normalized_relative_path);
        }
    }
}
