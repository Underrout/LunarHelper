using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

using LunarHelper.Resolvers;

namespace LunarHelper
{
    class DependencyResolver : IResolve<PatchRootVertex>, IToolRootResolve
    {
        private readonly PatchResolver patch_resolver;
        private readonly AmkResolver amk_resolver;
        private readonly PixiResolver pixi_resolver;
        private readonly GpsResolver gps_resolver;
        private readonly UberAsmResolver uberasm_resolver;

        public DependencyResolver(DependencyGraph graph, Config config)
        {
            if (!string.IsNullOrWhiteSpace(config.AsarPath) && config.Patches.Count != 0)
            {
                patch_resolver = new PatchResolver(graph, config.AsarPath, config.AsarOptions);
            }

            if (!string.IsNullOrWhiteSpace(config.AddMusicKPath))
            {
                amk_resolver = new AmkResolver(graph, config.AddMusicKPath);
            }

            if (!string.IsNullOrWhiteSpace(config.PixiPath))
            {
                pixi_resolver = new PixiResolver(graph, config.PixiPath, 
                    config.PixiOptions, config.OutputPath);
            }

            if (!string.IsNullOrWhiteSpace(config.GPSPath))
            {
                gps_resolver = new GpsResolver(graph, config.GPSPath, config.GPSOptions);
            }

            if (!string.IsNullOrWhiteSpace(config.UberASMPath))
            {
                uberasm_resolver = new UberAsmResolver(graph, config.UberASMPath, config.UberASMOptions);
            }
        }

        public bool CanResolvePatches()
        {
            return patch_resolver != null;
        }

        public bool CanResolveAmk()
        {
            return amk_resolver != null;
        }

        public bool CanResolvePixi()
        {
            return pixi_resolver != null;
        }

        public bool CanResolveGps()
        {
            return gps_resolver != null;
        }

        public bool CanResolveUberAsm()
        {
            return uberasm_resolver != null;
        }

        public void ResolveDependencies(PatchRootVertex vertex)
        {
            patch_resolver.ResolveDependencies(vertex);
        }

        public void ResolveToolRootDependencies(ToolRootVertex vertex)
        {
            switch (vertex.type)
            {
                case ToolRootVertex.Tool.Amk:
                    amk_resolver.ResolveToolRootDependencies(vertex);
                    break;

                case ToolRootVertex.Tool.Pixi:
                    pixi_resolver.ResolveToolRootDependencies(vertex);
                    break;

                case ToolRootVertex.Tool.Gps:
                    gps_resolver.ResolveToolRootDependencies(vertex);
                    break;

                case ToolRootVertex.Tool.UberAsm:
                    uberasm_resolver.ResolveToolRootDependencies(vertex);
                    break;
            }
        }
    }
}
