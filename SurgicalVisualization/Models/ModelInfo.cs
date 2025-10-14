using System;
using System.Windows.Media.Media3D;
namespace SurgicalVisualization.Models
{
    public class ModelInfo
    {
        public string FileName { get; }
        public TimeSpan LoadDuration { get; }
        public int TriangleCount { get; }
        public Rect3D BoundingBox { get; }
        public Point3D CenterOfMass { get; }
        public ModelInfo(string fileName, TimeSpan loadDuration, int triangleCount, Rect3D bbox, Point3D com)
        { FileName = fileName; LoadDuration = loadDuration; TriangleCount = triangleCount; BoundingBox = bbox; CenterOfMass = com; }
    }
}