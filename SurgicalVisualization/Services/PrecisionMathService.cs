using System;
using System.Windows.Media.Media3D;
namespace SurgicalVisualization.Services
{
    public static class PrecisionMathService
    {
        public static double Distance(Point3D a, Point3D b)
        {
            var dx = a.X - b.X; var dy = a.Y - b.Y; var dz = a.Z - b.Z;
            return Math.Sqrt(dx*dx + dy*dy + dz*dz);
        }
        public static double AngleBetween(Vector3D v1, Vector3D v2)
        {
            v1.Normalize(); v2.Normalize();
            var dot = Vector3D.DotProduct(v1, v2);
            dot = Math.Clamp(dot, -1.0, 1.0);
            return Math.Acos(dot) * 180.0 / Math.PI;
        }
        public static Rect3D ComputeBounds(Model3D model) => model.Bounds;
        public static Point3D CenterOfMass(MeshGeometry3D mesh)
        {
            double sx=0, sy=0, sz=0; int n = mesh.Positions.Count; if (n==0) return new Point3D();
            foreach (var p in mesh.Positions) { sx += p.X; sy += p.Y; sz += p.Z; }
            return new Point3D(sx/n, sy/n, sz/n);
        }
        public static int TriangleCount(MeshGeometry3D mesh) => mesh.TriangleIndices.Count / 3;
        public static Vector3D PrincipalAxis(Rect3D bbox)
        {
            var dx=bbox.SizeX; var dy=bbox.SizeY; var dz=bbox.SizeZ;
            if (dx>=dy && dx>=dz) return new Vector3D(1,0,0);
            if (dy>=dx && dy>=dz) return new Vector3D(0,1,0);
            return new Vector3D(0,0,1);
        }
        public static Quaternion AlignVectorTo(Vector3D from, Vector3D to)
        {
            from.Normalize(); to.Normalize();
            var axis = Vector3D.CrossProduct(from, to);
            var dot  = Vector3D.DotProduct(from, to);
            if (axis.Length == 0)
            {
                if (dot > 0.999999) return new Quaternion(new Vector3D(1,0,0), 0);
                var ortho = Math.Abs(from.X) < 0.9 ? new Vector3D(1,0,0) : new Vector3D(0,1,0);
                axis = Vector3D.CrossProduct(from, ortho); axis.Normalize();
                return new Quaternion(axis, 180);
            }
            var angle = Math.Acos(Math.Clamp(dot, -1.0, 1.0)) * 180.0 / Math.PI;
            axis.Normalize(); return new Quaternion(axis, angle);
        }
    }
}