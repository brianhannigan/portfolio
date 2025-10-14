using System.Windows.Media.Media3D;
namespace SurgicalVisualization.Models
{
    public class CalibrationData
    {
        public Vector3D XAxis { get; set; } = new Vector3D(1, 0, 0);
        public Vector3D YAxis { get; set; } = new Vector3D(0, 1, 0);
        public Vector3D ZAxis { get; set; } = new Vector3D(0, 0, 1);
        public Vector3D TargetVector { get; set; } = new Vector3D(0, 0, 1);
    }
}