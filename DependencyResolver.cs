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

        public DependencyResolver(DependencyGraph graph, Config config)
        {
            if (!string.IsNullOrWhiteSpace(config.AsarPath) && config.Patches.Count != 0)
            {
                patch_resolver = new PatchResolver(graph, Util.NormalizePath(config.AsarPath), config.AsarOptions);
            }

            if (!string.IsNullOrWhiteSpace(config.AddMusicKPath))
            {
                amk_resolver = new AmkResolver(graph, Util.NormalizePath(config.AddMusicKPath));
            }

            if (!string.IsNullOrWhiteSpace(config.PixiPath))
            {
                pixi_resolver = new PixiResolver(graph, Util.NormalizePath(config.PixiPath), 
                    config.PixiOptions, Util.NormalizePath(config.OutputPath));
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
                    throw new NotImplementedException();

                case ToolRootVertex.Tool.UberAsm:
                    throw new NotImplementedException();
            }
        }
    }
}
