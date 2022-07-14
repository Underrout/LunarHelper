using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LunarHelper
{
    using static DependencyGraphSerializer;

    class DependencyGraphAnalyzer
    {
        public enum Result
        {
            // indicates that compared subgraphs were identical
            Identical,

            // indicates that no old root was found and thus the new root is very new indeed
            NewRoot,

            // indicates no old or new root were found, i.e. the tool was not previously declared
            // and still isn't
            NoRoots,

            // indicates that an old root was present, but no new one
            OldRoot,

            // indicates that a modified file/modified files was/were found in the subgraph
            Modified,

            // indicates that a file in the subgraph is missing
            Missing,

            // indicates that an arbitrary file is part of the subgraph
            Arbitrary
        }

        // returns (true, null) if we have to suspect that an old patch was removed from the list
        // otherwise returns a list of patch root vertices and the result of analyzing their dependencies
        public static (bool, IEnumerable<(PatchRootVertex, (Result, IEnumerable<Vertex>))>) Analyze(DependencyGraph new_graph, IEnumerable<PatchRootVertex> new_roots, 
            IEnumerable<JsonVertex> old_graph)
        {
            var old_roots = old_graph.Where(v => v is JsonPatchRootVertex).Cast<JsonPatchRootVertex>();

            if (old_roots.Count() > new_roots.Count())
            {
                return (true, null);
            }

            var old_hashes_paths_and_vertices = old_roots.Select(r => (r.hash, r.path, r)).Cast<(string, string, JsonPatchRootVertex)>();
            var new_hashes_paths_and_vertices = new_roots.Select(r => (r.hash, r.normalized_relative_patch_path, r)).Cast<(string, string, PatchRootVertex)>();

            var old_dont_match_any_new = old_hashes_paths_and_vertices.Where(o =>
                !new_hashes_paths_and_vertices.Any(n => n.Item1 == o.Item1) &&
                !new_hashes_paths_and_vertices.Any(n => n.Item2 == o.Item2));

            if (old_dont_match_any_new.Count() != 0)
            {
                // we have at least one old patch which doesn't match any 
                // new patches by either path or hash, so we must assume that 
                // it was removed, which requires a reubild
                return (true, null);
            }

            var new_dont_match_any_old = new_hashes_paths_and_vertices.Where(n =>
                !old_hashes_paths_and_vertices.Any(o => o.Item1 == n.Item1) &&
                !old_hashes_paths_and_vertices.Any(o => o.Item2 == n.Item2));

            var remaining_new = new_hashes_paths_and_vertices.Where(n =>
                !new_dont_match_any_old.Select(t => t.Item3).Contains(n.Item3));

            IEnumerable<(PatchRootVertex, (Result, IEnumerable<Vertex>))> results = new List<(PatchRootVertex, (Result, IEnumerable<Vertex>))>();

            foreach (var match in remaining_new)
            {
                var old_root = old_roots.SingleOrDefault(r => r.hash == match.Item1 && r.path == match.Item2);
                if (old_root == null)
                {
                    old_root = old_roots.FirstOrDefault(r => r.hash == match.Item1 || r.path == match.Item2);
                }
                results = results.Append((match.Item3, CompareSubgraphs(new_graph, match.Item3, old_root, old_graph.ToList())));
            }

            foreach (var new_patch in new_dont_match_any_old)
            {
                results = results.Append((new_patch.Item3, (Result.NewRoot, new List<Vertex> { new_patch.Item3 })));
            }

            return (false, results);
        }

        public static (Result, IEnumerable<Vertex>) Analyze(DependencyGraph new_graph, ToolRootVertex.Tool tool, ToolRootVertex new_root, List<JsonVertex> old_graph)
        {
            var old_root = old_graph.SingleOrDefault(v => v is JsonToolRootVertex && ((JsonToolRootVertex)v).tool == tool);

            if (old_root == null)
            {
                if (new_root != null)
                {
                    return (Result.NewRoot, new List<Vertex> { new_root });
                }
                else
                {
                    return (Result.NoRoots, null);
                }
            }

            if (new_root == null)
            {
                return (Result.OldRoot, null);
            }

            return CompareSubgraphs(new_graph, new_root, old_root, old_graph);
        }

        private static (Result, IEnumerable<Vertex>) CompareSubgraphs(DependencyGraph new_graph, Vertex current_new_vertex, JsonVertex current_old_vertex,
            List<JsonVertex> old_graph, HashSet<Vertex> seen = null)
        {
            if (seen == null)
            {
                seen = new HashSet<Vertex>();
            }

            if (seen.Contains(current_new_vertex))
            {
                return (Result.Identical, null);
            }

            seen.Add(current_new_vertex);

            if (current_new_vertex is MissingFileOrDirectoryVertex)
            {
                return (Result.Missing, new List<Vertex> { current_new_vertex });
            }

            if (current_new_vertex is ArbitraryFileVertex)
            {
                return (Result.Arbitrary, new List<Vertex> { current_new_vertex });
            }

            var new_hash = current_new_vertex is HashFileVertex ? ((HashFileVertex)current_new_vertex).hash : null;
            var old_hash = current_old_vertex is JsonHashVertex ? ((JsonHashVertex)current_old_vertex).hash : null;

            if (new_hash != old_hash)
            {
                return (Result.Modified, new List<Vertex> { current_new_vertex });
            }

            if (current_new_vertex is HashFileNameVertex)
            {
                if (current_old_vertex is not JsonHashFileNameVertex)
                {
                    return (Result.Modified, new List<Vertex> { current_new_vertex });
                }
                else if (((JsonHashFileNameVertex)current_old_vertex).file_name != ((HashFileNameVertex)current_new_vertex).file_name)
                {
                    return (Result.Modified, new List<Vertex> { current_new_vertex });
                }
            }

            var new_dependencies = new_graph.dependency_graph.OutEdges(current_new_vertex)
                .Distinct()
                .OrderBy(e => e.Tag)
                .ThenBy(e => e.Target is HashFileNameVertex ? ((HashFileNameVertex)e.Target).file_name : "")
                .Select(e => (e.Source, e.Target is not GeneratedFileVertex ? e.Target : null, e.Tag));

            var old_dependencies = current_old_vertex.dependencies
                .Distinct()
                .OrderBy(d => d.tag)
                .ThenBy(d => d.idx >= 0 && old_graph[d.idx] is JsonHashFileNameVertex ? ((JsonHashFileNameVertex)old_graph[d.idx]).file_name : "")
                .Select(d => (current_old_vertex, d.idx >= 0 ? old_graph[d.idx] : null, d.tag));

            (var are_identical, var differing_vertex) = AreIdentical(new_dependencies, old_dependencies);

            if (!are_identical)
            {
                return (
                    differing_vertex is MissingFileOrDirectoryVertex ? Result.Missing : Result.Modified, 
                    new List<Vertex> { current_new_vertex, differing_vertex }
                );
            }

            // filtering out the generated vertices
            new_dependencies = new_dependencies.Where(t => t.Item2 != null);

            // filtering out generated, arbitrary and missing vertices
            old_dependencies = old_dependencies.Where(t => t.Item2 != null);

            foreach (var dependency in new_dependencies)
            {
                if (dependency.Item2 is ArbitraryFileVertex)
                {
                    return (Result.Arbitrary, new List<Vertex> { current_new_vertex, dependency.Item2 });
                }

                if (dependency.Item2 is MissingFileOrDirectoryVertex)
                {
                    return (Result.Missing, new List<Vertex> { current_new_vertex, dependency.Item2 });
                }
            }

            if (new_dependencies.Count() != old_dependencies.Count())
            {
                // if nothing fired in the foreach above, but we still ended up 
                // in here, it must mean that the old dependencies included 
                // arbitrary and/or missing vertices but the new ones don't
                return (Result.Modified, new List<Vertex> { current_new_vertex });
            }

            foreach ((var new_dependency, var old_dependency) in new_dependencies.Zip(old_dependencies))
            {
                (var result, var dependency_chain) = CompareSubgraphs(new_graph, new_dependency.Item2, old_dependency.Item2, old_graph);
                if (result != Result.Identical)
                {
                    return (result, dependency_chain.Prepend(current_new_vertex));
                }
            }

            return (Result.Identical, null);
        }

        private static (bool, Vertex) AreIdentical(IEnumerable<(Vertex, Vertex, string)> new_dependencies, 
            IEnumerable<(JsonVertex, JsonVertex, string)> old_dependencies)
        {
            if (new_dependencies.Count() != old_dependencies.Count())
            {
                return (false, null);
            }

            var new_cast = new_dependencies.Select(d => (d.Item2, d.Item2 is HashFileVertex ? ((HashFileVertex)d.Item2).hash : null, d.Item3));
            var old_cast = old_dependencies.Select(d => (d.Item2 is JsonHashVertex ? ((JsonHashVertex)d.Item2).hash : null, d.Item3));

            var first_mismatch = new_cast.FirstOrDefault(n => !old_cast.Contains((n.Item2, n.Item3))).Item1;

            return (first_mismatch == null, first_mismatch);
        }

        public static IEnumerable<Vertex> GetDependents(DependencyGraph graph, Vertex vertex)
        {
            return graph.dependency_graph.InEdges(vertex).Select(e => e.Source);
        }
    }
}
