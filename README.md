# FPSOverlay

FPS Overlay UI

## Add the overlay to multiple scenes

Open the tool from `Tools > Add FPS Overlay`. The window lists the scenes in Build Settings with their FPS overlay status. Use `Refresh status` to check all scenes, then add or remove the overlay on each scene separately. New overlays always start at 60 FPS; the tool does not save a frame-rate cap in the scene.

During play, the overlay measures every frame and refreshes its display every 0.3 seconds. Each refresh shows the slowest frame interval measured during that 0.3-second window, so brief frame drops are not missed between UI updates. FPS is calculated from that frame interval as `1 / frame time` (with frame time in seconds), capped at the selected target, and displayed to one decimal place. For example, a slowest frame time of `0.024 s` gives `1 / 0.024 = 41.7 FPS`, displayed as `41.7`; if all frames stay at or below the 60 FPS budget, FPS remains capped at `60.0`. This is neither an average across the UI refresh interval nor necessarily the very last frame. `Frame` shows the slowest measured frame against the target's time budget, calculated as `1000 / target FPS` in milliseconds (for example, `12.0/16.7 ms` at 60 FPS). `Main Thread` shows the highest Unity Profiler Main Thread CPU time sampled in that same window. A `SET` toggle opens a slider for 30, 60, 90, and 120 FPS. The target starts at 60 FPS and can be changed for the current play session only; it does not modify the scene. Frame interval and CPU time are separate measurements and can differ when the GPU or frame pacing is limiting performance.

The overlay panel, text, and controls use twice their previous size. Existing overlays are scaled at runtime as well.

Scenes that are already open are marked dirty when the overlay is added or removed, so save them in the editor. Scenes that are closed are opened temporarily, updated, saved, and closed by the tool.

## Installation

Download the Unity package from the [Releases](../../releases) page and import it using `Assets > Import Package > Custom Package...`.

When imported into a Git repository, the editor tool adds its `Assets/FPSOverlay` folder and folder metadata to that repository's local `.git/info/exclude`. The repository's shared `.gitignore` is not changed.
