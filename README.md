# VACAC Graduate Software Engineer Task 2026: Modular Conveyor System

A Unity application for building conveyor lines out of the provided conveyor models and
simulating the provided products travelling along them.

- **GitHub:** `https://github.com/<your-username>/VACAC-Conveyor-System`  ← replace
- **Build (Windows):** see the `v1.0` release on GitHub, or `VACAC_ConveyorSystem_Build.zip`
- **Video demo:** `<link or file name>`  ← replace

---

## How to run

1. Unzip `VACAC_ConveyorSystem_Build.zip`.
2. Double-click the `.exe`. No install is needed (Windows 10/11, 64-bit).

When it starts you'll see a demo line with products already running. Use the panel on the left to build more.

## Features

**Minimum requirements**
- ✅ Runs as a Windows build.
- ✅ **Select and place** any conveyor type from the panel (or keys 1–9), with a live preview under the mouse.
- ✅ **Snapping:** move the preview near any open conveyor end and it snaps exactly into place, in position, rotation and height.
  It works in both directions (extend the end of a line, or feed into the start of one).
- ✅ **Products move** smoothly along connected conveyors, including round curves.

**Extras**
- **Product queuing:** products keep a safe gap and queue up behind each other instead of overlapping.
- **Physics at line ends:** products fall off the end of a line, or off a conveyor that gets deleted, using real Rigidbody physics.
- **Automatic spawners:** every line start (a conveyor with nothing feeding it) spawns products, so several independent lines can run at once.
- **Edit tools:** click a placed conveyor to pick it up and move it, right-click or Delete to remove it, R to rotate, grid snapping (G), and Shift to place freely.
- **Placement validation:** the preview turns red and blocks placement if it would overlap another conveyor.
- **Simulation controls:** play/pause, belt speed slider, spawn rate slider, choose a product type or a random mix, and clear products.
- **Flow path visualisation:** a line along each belt (green = start, red = end) shows the direction of travel.
- **Save / Load layouts** to JSON, plus one-click **Demo line** and **Clear all**.
- **Statistics:** conveyors placed, line starts, products on belts, spawned and delivered counts.
- **Orbit camera:** pan, rotate, zoom, orbit, and reset view.
- **Generated curve conveyors:** the provided package contains straight and incline conveyors but no corner piece. The Setup Wizard procedurally builds 90° left and right curves (belt, side rails, support posts) that match the straight belt's width, height and materials, so lines can turn corners.
- **Inclines:** the incline model works as a slope, and products travel uphill.
- **Editor tooling:** a *Setup Wizard* (Tools → Conveyor System) turns raw models into configured conveyor and product prefabs and builds the whole scene in one click.
  It auto-detects belt start and end points from the mesh geometry, and turns one 90° curve model into both a left and a right piece.

## Controls

| Action | Input |
|---|---|
| Choose conveyor | Panel buttons or **1–9** |
| Place / pick up placed conveyor | **Left click** |
| Rotate preview | **R** (Shift+R = other way) |
| Place without snapping | Hold **Shift** |
| Toggle grid | **G** |
| Stop placing | **Right click** or **Esc** |
| Delete hovered conveyor | **Right click**, **Delete** or **X** |
| Pan / rotate camera | **WASD** or arrows / **Q E** |
| Zoom | Mouse wheel |
| Orbit | Middle-mouse drag or **Alt + Left drag** |
| Pan with mouse | **Shift + Middle drag** |
| Reset camera | **F** |
| Play / pause | **Space** |
| Hide UI panel | **H** |

## How it works (technical overview)

All code is in `Assets/ConveyorSystem`.

| Script | Responsibility |
|---|---|
| `ConveyorPiece` | One conveyor section. It has an **Entry** and an **Exit** transform (a position on the belt surface, with forward = travel direction). It builds a cubic Bézier path between them; the handle length is chosen so 90° pieces form a near-perfect circular arc and straights form a line. It keeps an arc-length table for constant-speed movement and links to its `Next` / `Previous` neighbours. |
| `ConveyorBuilder` | Build mode: preview ("ghost") piece, snapping, overlap checks, placing, moving, deleting, save/load and the demo line. |
| `Product` | Moves along the current piece by arc length and hands over to `Next` at the end. It looks ahead (across pieces) to queue behind the product in front, and switches to a dynamic Rigidbody when it leaves the line. |
| `SimulationManager` | Spawning at line starts, speed and spawn settings, statistics. |
| `BuilderUI` | On-screen panel (Unity IMGUI, so it needs no Canvas/EventSystem setup). |
| `CameraController` | Orbit / pan / zoom camera. |
| `LayoutStorage` | JSON save and load in `Application.persistentDataPath`. |
| `InputHelper` | Wraps input so the project works with either the legacy Input Manager or the new Input System. |
| `Editor/ConveyorSetupWizard` | Generates prefabs (adds colliders, snap points, components) and builds and saves the scene. |
| `Editor/CurveGenerator` | Procedurally builds the 90° curve meshes (a rectangular cross-section swept along a quarter circle, plus posts) and their prefabs. |
| `Editor/ConveyorAutoFit` | Finds the belt ends from the mesh: the long axis for straights. For 90° curves, it finds the two bounding-box faces that the mesh touches along a wide flat edge, then ray-casts down to find the belt surface height. It also lays models flat if they were exported standing on their end. |

**Snapping.** For every open end in the scene, the builder calculates the pose that puts the
preview's entry on that exit (or the preview's exit on that entry). The rotation is the
difference between the two travel directions' yaw angles. The position is the target point
minus the rotated local offset of the snap point. The candidate closest to the mouse
(within a radius) wins.
After placement, `RefreshConnections()` links any exit and entry that coincide and point the same way.

**Why path-following instead of pure physics?** Products driven by friction on a moving
collider are jittery, depend on the frame rate, and tend to slide off curves. Following
an arc-length-parameterised path gives smooth, deterministic motion at an exact belt speed,
makes queuing simple, and still hands over to physics when a product leaves the line.

## Development tools

- **Unity 6 LTS** (6000.0.x), Universal Render Pipeline
- **C#**, **Visual Studio Community 2022**
- **Git**, **GitHub**, **GitHub Desktop**
- **Claude Code** (Anthropic's AI coding assistant), used to help write and review the scripts
- Xbox Game Bar / OBS Studio for the video demo

## Known limitations and possible next steps

- Auto-detection of belt ends works for straight and 90° pieces. Other shapes need their Entry/Exit points placed by hand in the prefab.
- Planned next: undo/redo, junctions/diverters that sort products by type, elevated conveyors with supports, and a scrolling belt texture.
