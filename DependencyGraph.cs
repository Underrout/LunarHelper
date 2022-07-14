using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.IO;

using QuickGraph.Algorithms;
using QuickGraph;

namespace LunarHelper
{
    class DependencyGraph
    {
        public BidirectionalGraph<Vertex, STaggedEdge<Vertex, string>> dependency_graph;
        private DependencyResolver resolver;

        public ToolRootVertex pixi_root { get; } = null;
        public ToolRootVertex uberasm_root { get; } = null;
        public ToolRootVertex gps_root { get; } = null;
        public ToolRootVertex amk_root { get; } = null;
        public HashSet<Vertex> patch_roots = new HashSet<Vertex>();

        public DependencyGraph(Config config)
        {
            dependency_graph = new BidirectionalGraph<Vertex, STaggedEdge<Vertex, string>>();
            resolver = new DependencyResolver(this, config);

            if (resolver.CanResolveAmk())
            {
                amk_root = CreateToolRootVertex(ToolRootVertex.Tool.Amk);
                resolver.ResolveToolRootDependencies(amk_root);
            }

            if (resolver.CanResolvePixi())
            {
                pixi_root = CreateToolRootVertex(ToolRootVertex.Tool.Pixi);
                resolver.ResolveToolRootDependencies(pixi_root);
            }

            if (resolver.CanResolveGps())
            {
                gps_root = CreateToolRootVertex(ToolRootVertex.Tool.Gps);
                resolver.ResolveToolRootDependencies(gps_root);
            }

            if (resolver.CanResolveUberAsm())
            {
                uberasm_root = CreateToolRootVertex(ToolRootVertex.Tool.UberAsm);
                resolver.ResolveToolRootDependencies(uberasm_root);
            }

            if (resolver.CanResolvePatches())
            {
                CreatePatchRoots(config);

                foreach (var root in patch_roots)
                {
                    if (root is PatchRootVertex)
                    {
                        resolver.ResolveDependencies((PatchRootVertex)root);
                    }
                }
            }
        }

        private void CreatePatchRoots(Config config)
        {
            foreach (string user_specified_patch_path in config.Patches)
            {
                Vertex patch_root = null;

                try
                {
                    patch_root = new PatchRootVertex(user_specified_patch_path);
                }
                catch (NoUnderlyingFileException)
                {
                    patch_root = new MissingFileOrDirectoryVertex(Util.GetUri(user_specified_patch_path));
                }

                patch_roots.Add(patch_root);
                dependency_graph.AddVertex(patch_root);
            }
        }

        // will add a tagged edge between two vertices, even if another edge between them already exists
        public void AddEdge(Vertex source, Vertex target, string tag)
        {
            dependency_graph.AddEdge(new STaggedEdge<Vertex, string>(source, target, tag));
        }

        // will add a tagged edge between two vertices, but only if no edge exists between them yet
        // returns true if an edge was added and false if one was already found
        // if unique_tag = true, multiple edges between the vertex will be created as long as each 
        // has a unique tag
        public bool TryAddUniqueEdge(Vertex source, Vertex target, string tag, bool unique_tag = false)
        {
            if (dependency_graph.OutEdges(source).Any(e => e.Target == target && (!unique_tag || e.Tag == tag)))
            {
                return false;
            }

            dependency_graph.AddEdge(new STaggedEdge<Vertex, string>(source, target, tag));

            return true;
        }

        public ArbitraryFileVertex CreateArbitraryFileVertex()
        {
            ArbitraryFileVertex vertex = new ArbitraryFileVertex();
            dependency_graph.AddVertex(vertex);

            return vertex;
        }

        public Vertex GetOrCreateFileNameVertex(string file_path)
        {
            Uri uri = Util.GetUri(file_path);

            FileVertex maybe_vertex = dependency_graph.Vertices
                .Where(v => v is FileVertex)
                .Cast<FileVertex>()
                .SingleOrDefault(v => v.uri.Equals(uri));

            if (maybe_vertex != null)
            {
                return maybe_vertex;
            }

            Vertex new_vertex;

            try
            {
                new_vertex = new HashFileNameVertex(uri);
            }
            catch (NoUnderlyingFileException)
            {
                // this should now never happen with the !File.Exists guard at the top, probably 
                // remove this try/catch at some point
                new_vertex = new MissingFileOrDirectoryVertex(uri);
            }

            dependency_graph.AddVertex(new_vertex);

            return new_vertex;
        }

        public Vertex GetOrCreateMissingFileVertex(string file_path)
        {
            Uri uri = Util.GetUri(file_path);

            MissingFileOrDirectoryVertex maybe_vertex = dependency_graph.Vertices
                .Where(v => v is MissingFileOrDirectoryVertex)
                .Cast<MissingFileOrDirectoryVertex>()
                .SingleOrDefault(v => v.uri.Equals(uri));

            if (maybe_vertex != null)
            {
                return maybe_vertex;
            }

            MissingFileOrDirectoryVertex vertex = new MissingFileOrDirectoryVertex(uri);
            dependency_graph.AddVertex(vertex);

            return vertex;
        }

        private ToolRootVertex CreateToolRootVertex(ToolRootVertex.Tool for_tool)
        {
            ToolRootVertex vertex = new ToolRootVertex(for_tool);
            dependency_graph.AddVertex(vertex);
            return vertex;
        }

        // retrieves or creates a FileVertex for the provided file path
        //
        // if is_generated is set, whether an underlying file exists or not won't be 
        // checked and a GeneratedFileVertex will be returned, otherwise, if the 
        // underlying file exists, a HashFileVertex will be returned, if no such file
        // exists, a MissingFileVertex will be returned instead
        public Vertex GetOrCreateVertex(string file_path, bool is_generated = false, bool include_file_name_in_hash = false)
        {
            if (!File.Exists(file_path) && !is_generated)
            {
                return GetOrCreateMissingFileVertex(file_path);
            }

            Uri uri = Util.GetUri(file_path);

            FileVertex maybe_vertex = dependency_graph.Vertices
                .Where(v => v is FileVertex)
                .Cast<FileVertex>()
                .SingleOrDefault(v => v.uri.Equals(uri));

            if (maybe_vertex != null)
            {
                return maybe_vertex;
            }

            Vertex new_vertex;

            try
            {
                new_vertex = is_generated ? new GeneratedFileVertex(uri) : new HashFileVertex(uri);
            }
            catch (NoUnderlyingFileException)
            {
                // this should now never happen with the !File.Exists guard at the top, probably 
                // remove this try/catch at some point
                new_vertex = new MissingFileOrDirectoryVertex(uri);
            }

            dependency_graph.AddVertex(new_vertex);

            return new_vertex;
        }
    }
}
