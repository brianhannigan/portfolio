using Microsoft.Win32;
using System.IO;
namespace SurgicalVisualization.Helpers
{
    public static class FileDialogHelper
    {
        public static string? OpenModelDialog()
        {
            var ofd = new OpenFileDialog
            {
                Title = "Load 3D Model (STL/OBJ)",
                Filter = "3D Models (*.stl;*.obj)|*.stl;*.obj|STL (*.stl)|*.stl|OBJ (*.obj)|*.obj",
                CheckFileExists = true
            };
            return ofd.ShowDialog() == true ? ofd.FileName : null;
        }
        public static string EnsureLogsFolder()
        {
            var dir = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Logs");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return dir;
        }
    }
}