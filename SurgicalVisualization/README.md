# SurgicalVisualization (WPF, .NET, HelixToolkit.Wpf)

A precision-focused 3D visualization & control app simulating robot-assisted surgical guidance.  
Emphasis: deterministic math (double), traceable logging, MVVM, and thread-safe model loading.

---

## Tech Stack

- **.NET**: `net9.0-windows` (works on `net8.0-windows` too)
- **WPF** with MVVM (no code-behind logic except startup wiring)
- **HelixToolkit.Wpf** (high-fidelity 3D, STL/OBJ import)
- **System.Numerics** (quaternions, transforms)
- **Logging**: UTC ISO-8601, invariant numeric formatting

Packages:
- `HelixToolkit.Wpf`
- `CommunityToolkit.Mvvm`
- `System.Numerics.Vectors`

---

## Features (current)

- **Load STL/OBJ** models (bone, implant, instruments).
- **Interactive 3D**: rotate, pan, zoom (Helix viewport) with axis overlay.
- **Deterministic metrics**:
  - Triangle count
  - Axis-aligned bounding box (mm)
  - Center of mass (uniform density)
  - Load duration (ms)
- **Alignment**: rotate active model so its principal axis aligns to a target vector (0,0,1) via quaternion math.
- **Status & Telemetry**:
  - Live FPS
  - Camera position / look direction
- **Logging**: append-only file at `/Logs/system_log.txt`.
- **Export Screenshot**: PNG of the current viewport to `/Logs/`.

> **Note:** If you only see STL at the moment, OBJ still loads but materials depend on `.mtl` + textures being next to the `.obj`. See “Troubleshooting”.

---

## Project Layout

```
SurgicalVisualization/
├─ App.xaml / App.xaml.cs
├─ MainWindow.xaml / MainWindow.xaml.cs        # HelixViewport3D + UI
├─ ViewModels/
│   └─ MainViewModel.cs                       # MVVM commands, FPS, telemetry
├─ Models/
│   ├─ ModelInfo.cs
│   └─ CalibrationData.cs
├─ Services/
│   ├─ ModelLoader.cs                         # STA import + deep-freeze; metrics
│   ├─ PrecisionMathService.cs                # bbox, COM, angles, quaternions
│   └─ LoggerService.cs
├─ Helpers/
│   ├─ RelayCommand.cs
│   ├─ FileDialogHelper.cs
│   └─ StaTask.cs                             # runs import on STA thread
├─ Assets/
│   ├─ [sample.stl]
└─ Logs/
    └─ system_log.txt (created on first run)
```

---

## Build & Run

1. **Open** `SurgicalVisualization.sln` in Visual Studio 2022 or newer.
2. Make sure you have the **.NET desktop development** workload installed.
3. Target framework:
   - Recommended: `net9.0-windows` (VS with .NET 9 SDK)
   - Alternative: change `<TargetFramework>` to `net8.0-windows`
4. **Restore NuGet** packages, then **Build** and **Run**.
5. Use **Load Model** to open `.stl` or `.obj` files.

> If you have a `global.json` that doesn’t match your installed SDK, either delete it or update its `"sdk": { "version": ... }` to your local SDK (e.g., `9.0.201`).

---

## Using the App

### Mouse / Viewport
- **Rotate**: Left-drag
- **Pan**: Right-drag
- **Zoom**: Mouse wheel
- **Reset View**: Toolbar button (resets and Zoom Extents)
- **Axis Overlay**: Enabled by `ShowCoordinateSystem="True"` (lower-left triad)

### Workflow
1. **Load Model** → choose STL/OBJ
2. Inspect **Triangles / Bounding Box / COM / Load time** in the right panel.
3. **Align Device** → rotates the model’s principal axis to target vector `(0,0,1)` using quaternion rotation.  
   _Euler readouts are for display; the math uses quaternions._
4. **Export Screenshot** → saves `Logs/surgical_view_YYYYMMDD_HHMMSS.png`
5. Actions & metrics are logged to `Logs/system_log.txt` (UTC, invariant culture).

---

## Precision & Determinism

- All numeric computations use **double**.
- All logs/timestamps use **UTC ISO-8601** and **InvariantCulture**.
- Bounding box, center of mass, and triangle counts are derived from mesh data.
- Poses/angles are computed deterministically (no randomness).

---

## Thread-Safety (important)

WPF 3D types are `Freezable`s and must be created on an **STA** thread.  
**ModelLoader**:
- Imports STL/OBJ on a **dedicated STA thread**.
- Computes metrics on that same thread.
- **Deep-freezes** geometry, materials, brushes, and transforms.
- Returns a fully frozen `Model3DGroup`.
- ViewModel wraps the frozen model under a **mutable parent** to apply runtime transforms safely.

This design eliminates “The calling thread cannot access this object because a different thread owns it.”

---

## Troubleshooting

### “Only .stl shows / .obj not visible”
- Ensure the `.obj`’s **.mtl** file and all textures reside **next to the .obj** with correct relative paths.
- Try **Reset View** (Zoom Extents). Some models import far from origin or are very small/large.
- Check **triangle count** and **bbox** after load. If 0 or tiny, the OBJ may be empty or unit-scaled.
- Materials may load as untextured if textures are missing; geometry still displays.

### XAML errors (namespace / properties)
- Namespace should be the CLR mapping:
  ```xml
  xmlns:h="clr-namespace:HelixToolkit.Wpf;assembly=HelixToolkit.Wpf"
  ```
- The axis overlay is built-in: set `ShowCoordinateSystem="True"` on `<h:HelixViewport3D>`.  
  Do **not** add `CoordinateSystemVisual3D Size=...` — that varies by Helix version and may error.

### “Different thread owns it”
- You’re likely using a non-STA import or mutating a frozen model. This project’s loader already:
  - Runs import on **STA**,
  - **Deep-freezes** the model,
  - Applies transforms only to a **mutable wrapper**.  
  If you customized the loader, keep that pattern.

### Model not visible / black
- Try **Reset View**.
- Extremely large/small units can push the model out of a typical near/far plane; consider normalizing scales.
- If using OBJ+textures, missing textures may render dark materials; geometry still present.

---

## Controls & Metrics Shown

- **Active Model**: filename, triangle count, bounding box (mm), load duration, center of mass (mm)
- **Live Coordinates**: Euler (Pitch/Yaw/Roll) readout for UI; internal math uses quaternions
- **Calibration**: reference axes and alignment angle summary
- **FPS**: measured via WPF `CompositionTarget.Rendering`

---

## Roadmap (nice-to-have)

- Point picking + distance measurement (sub-mm)
- Multi-model sessions with per-model transforms
- Constraint cones and guide lines (`LinesVisual3D`)
- BVH-based collision/distance for tool-to-bone clearance
- Unit tests (IEC-style traceability) for geometry math

---

## License

For portfolio/demo use; add your preferred license here.
