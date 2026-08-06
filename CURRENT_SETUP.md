# Phantom Weight — Current Setup

A snapshot of how the project works **right now**. Code is the source of truth
if this file drifts.

---

## 1. Controls (Quest 3 with Touch controllers)

| Input | Action |
|-------|--------|
| **Left thumbstick** | Strafe / walk (head-relative) |
| **Right thumbstick horizontal** | Smooth turn |
| **Right thumbstick down** | Physical crouch |
| **Y button** (left controller) | Recenter — snaps tracking so the current head position reports the "normal" eye height (fixes "spawned too tall / too high") |
| **Grip (both hands) + swing arms** | Run in the direction you're facing. Requires **both** grips so single-hand grip stays for grabbing. |
| **Grip (one hand)** on a cube | Grab it (controller) |
| **Pinch** with hand-tracking on a cube | Grab it (bare hands) |
| **Grip** on the slider knob and drag left/right | Slide 0 % ↔ 100 % |

**Jump is intentionally not bound.** The old A-button jump stacked impulses off
cube contacts and read as flying — removed.

## 2. What the slider drives

`PlateFillPercent.percent` (0-100) fans out to:

1. **`Text (TMP)` on the knob** — `NN%` readout.
2. **`FillBar`** — colored strip on the track, blue → red.
3. **ESP32 payload** — on grab, `GrabDetector.Send("Lift,<pct>,<hand>")`; on release, `Release,0,<hand>`.

There is no more Dumbbell-mass hookup. If you want the dumbbell's `Rigidbody.mass`
to follow the slider again, wire it in the Inspector or add a small script.

## 3. Scripts (all in `Assets/Scripts/`)

| Script | Sits on | What it does |
|--------|---------|-------------|
| `CompleteVRLocomotion.cs` | `Player` (has `CharacterController`) | Walk, smooth turn, arm-swing run, physical crouch, physical move + height gain, recenter. **No jump.** |
| `playerPushScript.cs` | `Player` | Nudges cubes on contact (`pushPower = 0.15`). Also sets `CharacterController.stepOffset = 0.05` so the player **can't climb onto cubes** — they act as walls. |
| `PlateFillPercent.cs` | `slider/[BuildingBlock] Cube (2)` (the knob) | Reads knob world X → 0-100 %, drives label + fill bar |
| `GrabDetector.cs` | Every grabbable | Subscribes to `Grabbable` events; sends `Lift`/`Release` via `Esp32Bridge` |
| `Esp32Bridge.cs` | Static | One-time-open serial gateway (`COM4`, 115200) |
| `Billboard.cs` | Any world-space label | Faces the camera every frame |
| `WeightScaleCube.cs` | Demo cube | Scales itself with slider percent |
| `PokeLogger.cs` | Any button | Debug log on Meta SDK poke click |

## 4. ESP32 pipeline — where to edit what

Only two files touch the serial port.

| Change | File |
|--------|------|
| Payload format Unity sends | `Assets/Scripts/GrabDetector.cs` — the `Esp32Bridge.Send(...)` lines |
| COM port / baud rate | `Assets/Scripts/Esp32Bridge.cs` |
| What the ESP32 does with a message (GPIO, timing) | `Firmware/ESP32/ESP32.ino` |

Wire format:

```
Lift,<weight 0-100>,<Left|Right>
Release,0,<Left|Right>
```

## 5. Scene objects worth knowing

- **`Player`** — `CharacterController` + `CompleteVRLocomotion` + `playerPushScript`. Layer `Player` (6).
- **`Player/[BuildingBlock] Camera Rig`** — Meta's OVR rig. The `[BuildingBlock] Hand Tracking left/right` children under each hand anchor carry a **+0.06 m local Z offset** so hand-tracked meshes don't spawn on top of the visor.
- **`Dumbbell`** — `Grabbable` + `GrabInteractable` + `GrabFreeTransformer` + `Rigidbody` + `BoxCollider` + `GrabDetector`. Layer `Default`. **Keep the BoxCollider.** MeshColliders on Rigidbodies must be `Convex = true`, and a non-convex one makes the dumbbell fall through the floor and stops grabs from landing.
- **`[BuildingBlock] Cube` / `Cube (1)`** — same components as the dumbbell plus a `HandGrabInstallationRoutine` child for pinch grabs. Layer `Cubes` (8).
- **`slider/SliderPlate`** — the visible track.
- **`slider/[BuildingBlock] Cube (2)`** — the knob. `OneGrabTranslateTransformer` (X-only, relative constraints) + `Rigidbody` (kinematic, gravity off, `FreezePositionY | FreezePositionZ | FreezeRotation*`). Only the transformer can move it and only along X.
- **`slider/FillBar`** — colored fill strip driven by `PlateFillPercent.fillBar`.

## 6. Physics rules currently in place

- `Player` layer collides with everything (including `Cubes`).
- `CharacterController.stepOffset = 0.05` — cubes are walls, not steps.
- Slider knob's Rigidbody has `FreezePositionY | FreezePositionZ | FreezeRotation*`.
- Cube / dumbbell Rigidbodies: `Interpolate` + `Continuous` collision detection.

## 7. Common tuning (Inspector only)

| Object | Component | Field | Effect |
|--------|-----------|-------|--------|
| `Player` | `CompleteVRLocomotion` | `physicalMoveGain` | Horizontal step amplification. 1 = 1:1, 3 = 3× (default). |
| `Player` | `CompleteVRLocomotion` | `heightGain` | Vertical amplification while standing tall. Fades to 1× near the floor. |
| `Player` | `CompleteVRLocomotion` | `minSwing` | Combined hand speed (m/s) needed to run when using **controllers**. |
| `Player` | `CompleteVRLocomotion` | `minSwingHands` | Same threshold for **bare hand tracking** — set noticeably higher; hands report as swinging on any gesture. |
| `Player` | `CompleteVRLocomotion` | `recenterEyeHeight` | Head height (m) the recenter binding snaps you to. |
| `Player` | `playerPushScript` | `pushPower` | Impulse when the player capsule brushes a cube. |
| `Player` | `playerPushScript` | `stepOffset` | Written to `CharacterController.stepOffset` on Awake. Raise if you want small cubes climbable again. |
| Slider knob | `PlateFillPercent` | `fillColorLow` / `fillColorHigh` | Fill bar color ramp. |

## 8. Common issues

- **"Grab doesn't hold anything"** — the object needs `Grabbable` + `GrabInteractable` + a `Collider` sized to the mesh **and** an `IGrabTransformer` (usually `GrabFreeTransformer`). Missing the transformer is the silent-failure mode.
- **"Dumbbell falls through the floor when I add a MeshCollider"** — MeshColliders on Rigidbodies must have `Convex = true`. Even then, prefer the `BoxCollider` for grabs — it's cheaper and more reliable.
- **"Player climbs on top of blocks"** — `playerPushScript.stepOffset` is too high. Should be `≤ 0.05`.
- **"Player spawned too tall / floating"** — press **Y** on the left controller.
- **"Standing up makes me shorter in game"** — that's a sign the height-gain code has `-=` instead of `+=` on `_trackingSpaceBaseLocalY`. Fixed now.
- **"Bare hand tracking runs from any tiny motion"** — raise `minSwingHands` on the Player.
- **"Real hand appears inside the visor"** — bump the local Z on `[BuildingBlock] Hand Tracking left/right` above `0.06`.
- **"`COM4 unavailable`"** — ESP32 isn't plugged in. The game still runs.
- **"Missing script on Dumbbell"** — leftover `DumbbellWeight` component from before the file was deleted. Remove it in the Inspector (see below).

## 9. One-time Inspector cleanup after this update

The `DumbbellWeight.cs` file was deleted. If the `Dumbbell` GameObject still
has a component slot labeled `Missing (Mono Script)`, remove it:

1. Select `Dumbbell` in the Hierarchy.
2. In the Inspector, find the row that says `Missing (Mono Script)` (usually at the bottom).
3. Click the three-dot menu on that row → **Remove Component**.
4. Save the scene (Ctrl+S).

If you also want the dumbbell's mass to follow the slider again, either
add a tiny driver script or wire it manually in the Inspector.
