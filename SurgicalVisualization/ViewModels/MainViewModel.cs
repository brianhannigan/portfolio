using HelixToolkit.Wpf;
using SurgicalVisualization.Helpers;
using SurgicalVisualization.Models;
using SurgicalVisualization.Services;
using System;
using System.Diagnostics;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Threading.Tasks;
using System.Windows.Input;

namespace SurgicalVisualization.ViewModels
{
    public class MainViewModel : ObservableObjectShim
    {
        public Model3DGroup Scene { get; } = new Model3DGroup();
        private HelixViewport3D? _viewport;
        private readonly ModelLoader _loader = new ModelLoader();
        private readonly LoggerService _logger = new LoggerService();
        private readonly CalibrationData _calib = new CalibrationData();

        public RelayCommand LoadModelCommand { get; }
        public RelayCommand ResetViewCommand { get; }
        public RelayCommand AlignDeviceCommand { get; }
        public RelayCommand ExportLogCommand { get; }
        public RelayCommand ExportScreenshotCommand { get; }

        private string _statusMessage = "Ready";
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

        private double _fps;
        public double Fps { get => _fps; set => SetProperty(ref _fps, value); }

        private string _activeModelName="(none)";
        public string ActiveModelName { get => _activeModelName; private set => SetProperty(ref _activeModelName, value); }

        private int _triangleCount;
        public int TriangleCount { get => _triangleCount; private set => SetProperty(ref _triangleCount, value); }

        private string _bboxFormatted="Bounding Box: -";
        public string BoundingBoxFormatted { get => _bboxFormatted; private set => SetProperty(ref _bboxFormatted, value); }

        private string _loadDurFormatted="Load: -";
        public string LoadDurationFormatted { get => _loadDurFormatted; private set => SetProperty(ref _loadDurFormatted, value); }

        private string _comFormatted="COM: -";
        public string CenterOfMassFormatted { get => _comFormatted; private set => SetProperty(ref _comFormatted, value); }

        private string _posFormatted="X: 0.000  Y: 0.000  Z: 0.000";
        public string PositionFormatted { get => _posFormatted; private set => SetProperty(ref _posFormatted, value); }

        private string _rotFormatted="Pitch: 0.000  Yaw: 0.000  Roll: 0.000";
        public string RotationFormatted { get => _rotFormatted; private set => SetProperty(ref _rotFormatted, value); }

        private string _cursorFormatted="Cursor: -";
        public string CursorPositionFormatted { get => _cursorFormatted; private set => SetProperty(ref _cursorFormatted, value); }

        private string _orientFormatted="Orientation: -";
        public string OrientationFormatted { get => _orientFormatted; private set => SetProperty(ref _orientFormatted, value); }

        private string _calibSummary = string.Empty;
        public string CalibrationSummary { get => _calibSummary; private set => SetProperty(ref _calibSummary, value); }

        private string _alignAngleSummary = string.Empty;
        public string AlignmentAngleSummary { get => _alignAngleSummary; private set => SetProperty(ref _alignAngleSummary, value); }

        private Model3DGroup? _activeModel;
        private Transform3DGroup _activeTransform = new Transform3DGroup();
        private readonly Stopwatch _fpsSw = Stopwatch.StartNew();
        private int _frameCounter = 0;

        // === Orbit camera state ===
        private Point3D _orbitTarget = new Point3D(0, 0, 0);   // pivot point
        private const double OrbitBaseMove = 5.0;              // mm per key press (pan/zoom)
        private const double OrbitBaseRotDeg = 2.0;            // degrees per key press (yaw/pitch/roll)

        // Rotate vector v around 'axis' by angleDeg (Rodrigues' formula)
        private static Vector3D RotateAround(Vector3D v, Vector3D axis, double angleDeg)
        {
            if (axis.LengthSquared < 1e-12) return v;
            axis.Normalize();
            double rad = angleDeg * Math.PI / 180.0;
            double c = Math.Cos(rad), s = Math.Sin(rad);
            var cross = Vector3D.CrossProduct(axis, v);
            var dot = Vector3D.DotProduct(axis, v);
            return v * c + cross * s + axis * dot * (1 - c);
        }

        // Set the orbit pivot to the current model's bounding-box center
        private void SetOrbitTargetToModelCenter()
        {
            if (_activeModel is null) return;
            var b = _activeModel.Bounds;
            _orbitTarget = new Point3D(b.X + b.SizeX / 2.0, b.Y + b.SizeY / 2.0, b.Z + b.SizeZ / 2.0);
        }

        public MainViewModel()
        {
            LoadModelCommand = new RelayCommand(async _ => await LoadModelAsync());
            ResetViewCommand = new RelayCommand(_ => ResetView());
            AlignDeviceCommand = new RelayCommand(_ => AlignDeviceToTarget());
            ExportLogCommand = new RelayCommand(_ => ExportLog());
            ExportScreenshotCommand = new RelayCommand(_ => ExportScreenshot());
            CalibrationSummary = $"Ref Axes => X(1,0,0)  Y(0,1,0)  Z(0,0,1); Target: {_calib.TargetVector}";
            CompositionTarget.Rendering += CompositionTarget_Rendering;
        }

        public void AttachViewport(HelixViewport3D vp) { _viewport = vp; _logger.Info("Viewport attached."); }

        private void CompositionTarget_Rendering(object? sender, EventArgs e)
        {
            _frameCounter++;
            if (_fpsSw.ElapsedMilliseconds >= 500)
            {
                Fps = _frameCounter * 1000.0 / _fpsSw.ElapsedMilliseconds;
                _frameCounter = 0; _fpsSw.Restart();
            }
            if (_viewport is not null)
            {
                var cam = _viewport.Camera as ProjectionCamera;
                if (cam != null)
                {
                    OrientationFormatted = $"CamPos: {cam.Position.X:F3},{cam.Position.Y:F3},{cam.Position.Z:F3}  " +
                                           $"LookDir: {cam.LookDirection.X:F3},{cam.LookDirection.Y:F3},{cam.LookDirection.Z:F3}";
                }
            }
        }

        private async Task LoadModelAsync()
        {
            try
            {
                var path = Helpers.FileDialogHelper.OpenModelDialog();
                if (string.IsNullOrWhiteSpace(path)) return;

                StatusMessage = "Loading model...";
                _logger.Info($"LoadModel start\tfile={path}");

                var (frozenGroup, info) = await _loader.LoadAsync(path); // frozen, STA-imported

                // Wrap the frozen model in a mutable parent for transforms
                _activeTransform = new Transform3DGroup();
                var wrapper = new Model3DGroup { Transform = _activeTransform };
                wrapper.Children.Add(frozenGroup);

                RemovePreviousModel();
                Scene.Children.Add(wrapper);
                _activeModel = wrapper;
                SetOrbitTargetToModelCenter();   // pivot = model center


                ActiveModelName = info.FileName;
                TriangleCount = info.TriangleCount;
                BoundingBoxFormatted = $"Bounding Box (mm): X={info.BoundingBox.SizeX:F3}  Y={info.BoundingBox.SizeY:F3}  Z={info.BoundingBox.SizeZ:F3}";
                LoadDurationFormatted = $"Load: {info.LoadDuration.TotalMilliseconds:F1} ms";
                CenterOfMassFormatted = $"COM (mm): {info.CenterOfMass.X:F3}, {info.CenterOfMass.Y:F3}, {info.CenterOfMass.Z:F3}";

                PositionFormatted = "X: 0.000  Y: 0.000  Z: 0.000";
                RotationFormatted = "Pitch: 0.000  Yaw: 0.000  Roll: 0.000";

                StatusMessage = "Model loaded.";
                _logger.Info($"LoadModel complete\tfile={info.FileName}\ttri={info.TriangleCount}\tbbox=({info.BoundingBox.SizeX:F3},{info.BoundingBox.SizeY:F3},{info.BoundingBox.SizeZ:F3})\tloadms={info.LoadDuration.TotalMilliseconds:F1}");

                _viewport?.ZoomExtents();
            }
            catch (Exception ex)
            {
                _logger.Error("LoadModel failed: " + ex);
                StatusMessage = "Error loading model.";
                MessageBoxShim.Show("Error loading model: " + ex.Message, "Load Error");
            }
        }

        // === Paste these inside MainViewModel class ===

        // Rotate vector v around 'axis' by angleDeg (Rodrigues' rotation formula)
        //private static Vector3D RotateAround(Vector3D v, Vector3D axis, double angleDeg)
        //{
        //    if (axis.LengthSquared < 1e-12) return v;
        //    axis.Normalize();
        //    double rad = angleDeg * Math.PI / 180.0;
        //    double c = Math.Cos(rad), s = Math.Sin(rad);
        //    var cross = Vector3D.CrossProduct(axis, v);
        //    var dot = Vector3D.DotProduct(axis, v);
        //    return v * c + cross * s + axis * dot * (1 - c);
        //}

        // Central handler for WASD/QE/arrows/+/-
        public void HandleGameKey(KeyEventArgs e)
        {
            if (_viewport?.Camera is not ProjectionCamera cam) return;

            // Camera basis from current orientation
            var look = cam.LookDirection; if (look.LengthSquared < 1e-12) look = new Vector3D(0, 0, -1);
            var up = cam.UpDirection; if (up.LengthSquared < 1e-12) up = new Vector3D(0, 0, 1);
            look.Normalize();
            up.Normalize();
            var right = Vector3D.CrossProduct(look, up);
            if (right.LengthSquared < 1e-12) right = new Vector3D(1, 0, 0);
            right.Normalize();

            bool fast = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
            double move = fast ? OrbitBaseMove * 5.0 : OrbitBaseMove;
            double rot = fast ? OrbitBaseRotDeg * 3.0 : OrbitBaseRotDeg;

            // Vector from target to camera, and current distance
            Vector3D toCam = cam.Position - _orbitTarget;
            double dist = toCam.Length;
            if (dist < 1e-9) { toCam = -look; dist = toCam.Length; if (dist < 1e-9) dist = 1.0; }

            bool handled = true;

            switch (e.Key)
            {
                // ----- Orbit rotations around target -----
                // Arrows: yaw/pitch the camera position around the pivot; keep target fixed.
                case Key.Left:   // yaw left about UP
                    toCam = RotateAround(toCam, up, rot);
                    look = _orbitTarget - (cam.Position = _orbitTarget + toCam);
                    break;
                case Key.Right:  // yaw right
                    toCam = RotateAround(toCam, up, -rot);
                    look = _orbitTarget - (cam.Position = _orbitTarget + toCam);
                    break;
                case Key.Up:     // pitch up about RIGHT
                    toCam = RotateAround(toCam, right, rot);
                    look = _orbitTarget - (cam.Position = _orbitTarget + toCam);
                    // recompute 'up' to remain orthonormal
                    right = Vector3D.CrossProduct(look, up); right.Normalize();
                    up = Vector3D.CrossProduct(right, look); up.Normalize();
                    break;
                case Key.Down:   // pitch down
                    toCam = RotateAround(toCam, right, -rot);
                    look = _orbitTarget - (cam.Position = _orbitTarget + toCam);
                    right = Vector3D.CrossProduct(look, up); right.Normalize();
                    up = Vector3D.CrossProduct(right, look); up.Normalize();
                    break;

                // Roll camera about line of sight
                case Key.PageUp:     // roll CCW
                    up = RotateAround(up, look, rot); up.Normalize();
                    break;
                case Key.PageDown:   // roll CW
                    up = RotateAround(up, look, -rot); up.Normalize();
                    break;

                // ----- Dolly (zoom) toward/away from target -----
                // + / - and W/S: change distance to target along the viewing direction
                case Key.Add:
                case Key.OemPlus:
                case Key.W:
                    {
                        double step = move;
                        double newDist = Math.Max(0.1, dist - step);
                        // Move camera along the current look vector toward target
                        var dirToTarget = (-toCam); dirToTarget.Normalize();
                        cam.Position = _orbitTarget - dirToTarget * newDist;
                        look = _orbitTarget - cam.Position;
                    }
                    break;

                case Key.Subtract:
                case Key.OemMinus:
                case Key.S:
                    {
                        double step = move;
                        double newDist = dist + step;
                        var dirToTarget = (-toCam); dirToTarget.Normalize();
                        cam.Position = _orbitTarget - dirToTarget * newDist;
                        look = _orbitTarget - cam.Position;
                    }
                    break;

                // ----- Pan (A/D = left/right, Q/E = down/up) -> move BOTH position and target -----
                case Key.A: // pan left
                    cam.Position -= right * move;
                    _orbitTarget -= right * move;
                    break;
                case Key.D: // pan right
                    cam.Position += right * move;
                    _orbitTarget += right * move;
                    break;
                case Key.Q: // pan down
                    cam.Position -= up * move;
                    _orbitTarget -= up * move;
                    break;
                case Key.E: // pan up
                    cam.Position += up * move;
                    _orbitTarget += up * move;
                    break;

                default:
                    handled = false;
                    break;
            }

            if (handled)
            {
                // Keep camera vectors consistent
                look.Normalize();
                cam.LookDirection = look;
                up.Normalize();
                cam.UpDirection = up;

                // Update readout
                OrientationFormatted = $"CamPos: {cam.Position.X:F3},{cam.Position.Y:F3},{cam.Position.Z:F3}  " +
                                       $"LookDir: {cam.LookDirection.X:F3},{cam.LookDirection.Y:F3},{cam.LookDirection.Z:F3}";

                e.Handled = true;
            }
        }



        private void RemovePreviousModel()
        {
            var toRemove = Scene.Children.OfType<Model3DGroup>().ToList();
            foreach (var m in toRemove) Scene.Children.Remove(m);
        }

        private void ResetView()
        {
            try
            {
                if (_viewport != null)
                {
                    _viewport.CameraController?.ResetCamera();
                    _viewport.ZoomExtents();
                    SetOrbitTargetToModelCenter();

                    _logger.Info("ResetView invoked.");
                    StatusMessage = "View reset.";
                }
            }
            catch (Exception ex) { _logger.Error("ResetView error: " + ex); }
        }

        private void AlignDeviceToTarget()
        {
            if (_activeModel is null) { MessageBoxShim.Show("Load a model first.", "Align Device"); return; }
            try
            {
                var bbox = _activeModel.Bounds;
                var principal = PrecisionMathService.PrincipalAxis(bbox);
                var q = PrecisionMathService.AlignVectorTo(principal, _calib.TargetVector);
                var rt = new RotateTransform3D(new QuaternionRotation3D(q));
                _activeTransform.Children.Add(rt);
                var euler = QuaternionToEuler(q);
                RotationFormatted = $"Pitch: {euler.X:F3}  Yaw: {euler.Y:F3}  Roll: {euler.Z:F3}";
                AlignmentAngleSummary = $"Aligned principal axis to Target; Δθ≈{q.Angle:F3}°";
                _logger.Info($"AlignDevice principal={principal} target={_calib.TargetVector} angle={q.Angle:F3}");
                StatusMessage = "Device aligned to target.";
            }
            catch (Exception ex)
            {
                _logger.Error("AlignDevice error: " + ex);
                MessageBoxShim.Show("Alignment failed: " + ex.Message, "Align Error");
            }
        }

        private static Vector3D QuaternionToEuler(Quaternion q)
        {
            double ysqr = q.Y * q.Y;
            double t0 = +2.0 * (q.W * q.X + q.Y * q.Z);
            double t1 = +1.0 - 2.0 * (q.X * q.X + ysqr);
            double roll = Math.Atan2(t0, t1);
            double t2 = +2.0 * (q.W * q.Y - q.Z * q.X);
            t2 = Math.Clamp(t2, -1.0, 1.0);
            double pitch = Math.Asin(t2);
            double t3 = +2.0 * (q.W * q.Z + q.X * q.Y);
            double t4 = +1.0 - 2.0 * (ysqr + q.Z * q.Z);
            double yaw = Math.Atan2(t3, t4);
            return new Vector3D(roll * 180.0/Math.PI, pitch * 180.0/Math.PI, yaw * 180.0/Math.PI);
        }

        private void ExportLog()
        {
            _logger.Info("User requested ExportLog (file at /Logs/system_log.txt)");
            StatusMessage = "Log written to /Logs/system_log.txt";
        }

        private void ExportScreenshot()
        {
            try
            {
                if (_viewport is null) { MessageBoxShim.Show("Viewport not available.", "Export Screenshot"); return; }
                var rtb = new RenderTargetBitmap((int)_viewport.ActualWidth, (int)_viewport.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(_viewport);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtb));
                var dir = Helpers.FileDialogHelper.EnsureLogsFolder();
                var path = System.IO.Path.Combine(dir, $"surgical_view_{DateTime.UtcNow:yyyyMMdd_HHmmss}.png");
                using (var fs = System.IO.File.Create(path)) encoder.Save(fs);
                _logger.Info($"ExportScreenshot path={path}");
                StatusMessage = "Screenshot exported.";
            }
            catch (Exception ex)
            {
                _logger.Error("ExportScreenshot error: " + ex);
                MessageBoxShim.Show("Screenshot failed: " + ex.Message, "Screenshot Error");
            }
        }
    }

    public abstract class ObservableObjectShim : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        protected bool SetProperty<T>(ref T storage, T value, string? name = null)
        {
            if (Equals(storage, value)) return false;
            storage = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name ?? GetCallerName()));
            return true;
        }
        private string GetCallerName([System.Runtime.CompilerServices.CallerMemberName] string name = "") => name;
    }
    public static class MessageBoxShim
    {
        public static void Show(string text, string caption) => System.Windows.MessageBox.Show(text, caption);
    }
}