# FPSOverlay

FPS Overlay UI

## Add the overlay to multiple scenes

Open the tool from `Tools > Add FPS Overlay`. The window lists the scenes in Build Settings with their FPS overlay status. Use `Refresh status` to check all scenes, then add or remove the overlay on each scene separately. New overlays always start at 60 FPS; the tool does not save a frame-rate cap in the scene.

During play, the overlay measures each frame and refreshes its display every 0.3 seconds. It shows FPS calculated from the most recently measured frame as `1 / Time.unscaledDeltaTime` (with delta time in seconds), rounded to a whole number and capped at the selected target. For example, a frame time of `0.024 s` gives `1 / 0.024 = 41.7 FPS`, displayed as `42`; if a frame measures 76 FPS while the target is 60, the overlay displays 60. This is not an average across the 0.3-second UI refresh interval. The overlay also shows that frame's duration against the selected target's time budget, calculated as `1000 / target FPS` in milliseconds (for example, `12.0/16.7 ms` at 60 FPS), and Unity Profiler `Main Thread` CPU time. A `SET` toggle opens a slider for 30, 60, 90, and 120 FPS. The target starts at 60 FPS and can be changed for the current play session only; it does not modify the scene. Frame interval and CPU time are separate measurements and can differ when the GPU or frame pacing is limiting performance.

The overlay panel, text, and controls use twice their previous size. Existing overlays are scaled at runtime as well.

Scenes that are already open are marked dirty when the overlay is added or removed, so save them in the editor. Scenes that are closed are opened temporarily, updated, saved, and closed by the tool.

## Installation

Download the Unity package from the [Releases](../../releases) page and import it using `Assets > Import Package > Custom Package...`.

When imported into a Git repository, the editor tool adds its `Assets/FPSOverlay` folder and folder metadata to that repository's local `.git/info/exclude`. The repository's shared `.gitignore` is not changed.
