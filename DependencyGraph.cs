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
    class CriticalDependencyMissingException : Exception
    {
        public CriticalDependencyMissingException()
        {

        }

        public CriticalDependencyMissingException(string file_path) :
            base($"Required dependency \"{file_path}\" is missing, cannot build")
        {

        }

        public CriticalDependencyMissingException(string message, string file_path) :
            base(String.Format(message, file_path))
        {

        }
    }

    class DependencyGraph
    {
        public BidirectionalGraph<Vertex, STaggedEdge<Vertex, string>> dependency_graph;
        private DependencyResolver resolver;

        public ToolRootVertex pixi_root { get; } = null;
        // private HashSet<Vertex> uberasm_sources;
        // private HashSet<Vertex> gps_sources;
        public ToolRootVertex amk_root { get; } = null;
        public HashSet<PatchRootVertex> patch_roots = new HashSet<PatchRootVertex>();

        // public DependencyGraph(ICollection<string> pixi_sources, ICollection<string> uberasm_sources, ICollection<string> gps_sources,
        //      ICollection<string> amk_sources, ICollection<string> patch_sources)
        public DependencyGraph(Config config)
        {
            dependency_graph = new BidirectionalGraph<Vertex, STaggedEdge<Vertex, string>>();
            resolver = new DependencyResolver(this, config);

            // this.pixi_sources = CreateSourceSet(pixi_sources);
            // this.uberasm_sources = CreateSourceSet(uberasm_sources);
            // this.gps_sources = CreateSourceSet(gps_sources);

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

            if (resolver.CanResolvePatches())
            {
                CreatePatchRoots(config);

                foreach (var root in patch_roots)
                {
                    resolver.ResolveDependencies(root);
                }
            }
        }

        private void CreatePatchRoots(Config config)
        {
            foreach (string user_specified_patch_path in config.Patches)
            {
                PatchRootVertex patch_root = new PatchRootVertex(user_specified_patch_path);

                if (patch_root is not FileVertex)
                {
                    throw new CriticalDependencyMissingException(
                        "User-specified patch \"{0}\" was not found", user_specified_patch_path);
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
        public bool TryAddUniqueEdge(Vertex source, Vertex target, string tag)
        {
            if (dependency_graph.OutEdges(source).Any(e => e.Target == target))
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

        public Vertex GetOrCreateMissingFileVertex(string file_path)
        {
            FileVertex maybe_vertex = dependency_graph.Vertices
                .Where(v => v is FileVertex)
                .Cast<FileVertex>()
                .SingleOrDefault(v => Util.PathsEqual(v.normalized_file_path, file_path));

            if (maybe_vertex != null)
            {
                return maybe_vertex;
            }

            MissingFileVertex vertex = new MissingFileVertex(file_path);
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
        public Vertex GetOrCreateVertex(string file_path, bool is_generated = false)
        {
            FileVertex maybe_vertex = dependency_graph.Vertices
                .Where(v => v is FileVertex)
                .Cast<FileVertex>()
                .SingleOrDefault(v => Util.PathsEqual(v.normalized_file_path, file_path));

            if (maybe_vertex != null)
            {
                return maybe_vertex;
            }

            Vertex new_vertex;

            try
            {
                new_vertex = is_generated ? new GeneratedFileVertex(file_path) : new HashFileVertex(file_path);
            }
            catch (NoUnderlyingFileException)
            {
                new_vertex = new MissingFileVertex(file_path);
            }

            dependency_graph.AddVertex(new_vertex);

            return new_vertex;
        }
    }
}
