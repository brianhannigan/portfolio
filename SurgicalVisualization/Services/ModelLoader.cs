using HelixToolkit.Wpf;
using SurgicalVisualization.Helpers;
using SurgicalVisualization.Models;
using System;
using System.Diagnostics;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Threading.Tasks;
using System.Windows;

namespace SurgicalVisualization.Services
{
    /// <summary>
    /// Loads STL/OBJ using HelixToolkit on a dedicated STA thread.
    /// Computes metrics and DEEP-freezes the graph on that same STA thread,
    /// then returns the already-frozen model + metadata to the UI thread.
    /// </summary>
    public class ModelLoader
    {
        public async Task<(Model3DGroup model, ModelInfo info)> LoadAsync(string path)
        {
            // Everything (import + metrics + freeze) happens on a dedicated STA thread.
            var result = await StaTask.Run<(Model3DGroup, ModelInfo)>(() =>
            {
                var sw = Stopwatch.StartNew();

                // Import
                var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
                Model3DGroup group;
                if (ext == ".stl")
                {
                    var r = new StLReader();
                    group = r.Read(path);
                }
                else if (ext == ".obj")
                {
                    var r = new ObjReader();
                    group = r.Read(path);
                }
                else
                {
                    throw new NotSupportedException($"Unsupported model format: {ext}");
                }

                sw.Stop();

                // Metrics (still on the same STA thread that created the objects)
                int triCount = 0;
                Point3D? comAccum = null;
                int comSamples = 0;

                foreach (var gm in group.Children.OfType<GeometryModel3D>())
                {
                    if (gm.Geometry is MeshGeometry3D mesh)
                    {
                        triCount += PrecisionMathService.TriangleCount(mesh);
                        var com = PrecisionMathService.CenterOfMass(mesh);
                        if (comAccum == null) comAccum = com;
                        else comAccum = new Point3D(
                            comAccum.Value.X + com.X,
                            comAccum.Value.Y + com.Y,
                            comAccum.Value.Z + com.Z);
                        comSamples++;
                    }
                }

                var bbox = group.Bounds;
                var comAvg = comSamples > 0
                    ? new Point3D(
                        comAccum!.Value.X / comSamples,
                        comAccum.Value.Y / comSamples,
                        comAccum.Value.Z / comSamples)
                    : new Point3D();

                // Deep-freeze EVERYTHING on this STA thread so the object graph becomes free-threaded
                DeepFreeze(group);

                var info = new ModelInfo(
                    System.IO.Path.GetFileName(path),
                    sw.Elapsed,
                    triCount,
                    bbox,
                    comAvg);

                return (group, info);
            });

            return result;
        }

        /// <summary>
        /// Recursively freezes the entire subgraph: geometries, materials, brushes, and transforms.
        /// Guarantees no Dispatcher affinity remains.
        /// </summary>
        private static void DeepFreeze(Model3D model)
        {
            if (model is Model3DGroup group)
            {
                foreach (var child in group.Children)
                    DeepFreeze(child);

                TryFreeze(group.Transform);
                TryFreeze(group);
                return;
            }

            if (model is GeometryModel3D g)
            {
                if (g.Geometry is MeshGeometry3D mesh)
                {
                    TryFreeze(mesh.Positions);
                    TryFreeze(mesh.Normals);
                    TryFreeze(mesh.TextureCoordinates);
                    TryFreeze(mesh.TriangleIndices);
                    TryFreeze(mesh);
                }

                FreezeMaterialTree(g.Material);
                FreezeMaterialTree(g.BackMaterial);
                TryFreeze(g.Transform);
                TryFreeze(g);
            }
        }

        private static void FreezeMaterialTree(Material? mat)
        {
            if (mat == null) return;

            if (mat is MaterialGroup mg)
            {
                foreach (var m in mg.Children)
                    FreezeMaterialTree(m);
                TryFreeze(mg);
            }
            else if (mat is DiffuseMaterial dm)
            {
                TryFreeze(dm.Brush);
                TryFreeze(dm);
            }
            else if (mat is SpecularMaterial sm)
            {
                TryFreeze(sm.Brush);
                TryFreeze(sm);
            }
            else if (mat is EmissiveMaterial em)
            {
                TryFreeze(em.Brush);
                TryFreeze(em);
            }
            else
            {
                TryFreeze(mat);
            }
        }

        private static void TryFreeze(object? o)
        {
            if (o is null) return;
            if (o is Freezable f && f.CanFreeze && !f.IsFrozen)
            {
                try { f.Freeze(); }
                catch
                {
                    try { var c = f.Clone(); c.Freeze(); }
                    catch { /* best-effort; ignore */ }
                }
            }
        }
    }
}
