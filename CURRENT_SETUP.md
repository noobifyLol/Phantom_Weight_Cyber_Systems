# Phantom Weight — Current Setup

This is a snapshot of how the project works **right now**. No history, no
change log — just what each script does, what's in the scene, and what
button does what. If something behaves differently than described here,
the code is the source of truth.

---

## 1. Controls (Quest 3 with Touch controllers)

| Input | Action |
|-------|--------|
| **Left thumbstick** | Strafe / walk (head-relative) |
| **Right thumbstick horizontal** | Smooth turn |
| **Right thumbstick down** | Physical crouch |
| **A button** (right controller) | Jump |
| **Y button** (left controller) | Recenter — snaps tracking so your current head position reports the "normal" eye height (fixes "spawned too tall / too high") |
| **Grip (both hands) + swing arms** | Run in the direction you're facing. Speed follows how fast you swing. Requires **both** grips so a single-hand grip stays reserved for grabbing. |
| **Grip (one hand)** on a cube or the dumbbell | Grab it (controller-based) |
| **Pinch** with hand-tracking on a cube or the dumbbell | Grab it (hand-tracked) |
| **Grip** on the slider knob and drag left/right | Slide the calibration knob 0 % ↔ 100 % |

## 2. What the slider drives

One slider, three outputs, all read from `PlateFillPercent.percent` (0-100).

1. **`Text (TMP)` on the knob** shows `NN%`.
2. **`FillBar`** — colored strip along the slider track that grows from 0 to
   full length as `percent` goes up. Colors ramp blue → red.
3. **`Dumbbell` Rigidbody mass** — 0.5 kg at 0 %, 12 kg at 100 % (tune in the
   `DumbbellWeight` inspector). Meta's Grabbable un-kinematics on release, so
   the mass shows up when you swing / throw / release the dumbbell.
4. **ESP32 payload** — when you grab something, `GrabDetector.Send("Lift,<pct>,<hand>")` fires
   with the current percent as the weight. When you let go, `Release,0,<hand>` fires.

The dumbbell's visible material is whatever the `Part_1 / Part_2 / Part_3`
meshes shipped with (currently `black.mat`, `Part_2Mat`, `black.mat`).
`DumbbellWeight.tintTargets` is empty by design — the script does not
overwrite the material.

## 3. Scripts (all in `Assets/Scripts/`)

| Script | Sits on | What it does |
|--------|---------|-------------|
| `CompleteVRLocomotion.cs` | `Player` (has `CharacterController`) | Movement — joystick walk, smooth turn, arm-swing run, jump, physical crouch, physical-move gain, recenter |
| `playerPushScript.cs` | `Player` | Gives cubes a *tiny* impulse (`pushPower = 0.15`) on contact, so walking into them nudges them. Also sets `CharacterController.stepOffset` so small cubes are climbable. |
| `PlateFillPercent.cs` | `slider/[BuildingBlock] Cube (2)` (the knob) | Reads knob world X, computes 0-100 %, updates `percentLabel` and `fillBar` |
| `DumbbellWeight.cs` | `Dumbbell` | Reads slider percent, writes `Rigidbody.mass` (and can tint / show a label if wired) |
| `GrabDetector.cs` | Every grabbable (cubes + dumbbell + slider knob) | Subscribes to `Grabbable.WhenPointerEventRaised`; sends `Lift`/`Release` through `Esp32Bridge` |
| `Esp32Bridge.cs` | Static | One-time-open serial gateway to the ESP32 (`COM4` default, 115200) |
| `Billboard.cs` | Any world-space label | Rotates to face the camera every frame |
| `WeightScaleCube.cs` | Demo cube | Scales itself with slider percent — leftover from an earlier weight demo |
| `PokeLogger.cs` | Any button | Debug log on Meta SDK poke click |

## 4. Where to edit the ESP32 pipeline

Only two files. Nothing else in the codebase touches the serial port.

| Change | File |
|--------|------|
| What Unity sends over serial (payload format) | `Assets/Scripts/GrabDetector.cs` — the two `Esp32Bridge.Send(...)` lines |
| COM port / baud rate | `Assets/Scripts/Esp32Bridge.cs` — `PortName` / `BaudRate` |
| What the ESP32 does with a message (GPIO, timing) | `Firmware/ESP32/ESP32.ino` |

Message format on the wire:

```
Lift,<weight 0-100>,<Left|Right>
Release,0,<Left|Right>
```

## 5. Key scene objects

- **`Player`** — the moving rig. `CharacterController` + `CompleteVRLocomotion` + `playerPushScript`. Layer `Player` (6).
- **`Player/[BuildingBlock] Camera Rig`** — Meta's OVR rig (headset, hand anchors, controllers). Contains `[BuildingBlock] Hand Tracking left/right` under the hand anchors — those have a **+0.06 m local Z offset** so hand-tracked meshes don't spawn on top of the visor.
- **`Dumbbell`** — `Grabbable` + `GrabInteractable` + `GrabFreeTransformer` (needed for controller grab to hold anything) + `Rigidbody` + `BoxCollider` sized to the mesh + `DumbbellWeight` + `GrabDetector`. Layer `Default`.
- **`[BuildingBlock] Cube` / `Cube (1)`** — same components as the dumbbell, plus a `HandGrabInstallationRoutine` child for pinch grabs. Layer `Cubes` (8).
- **`slider/SliderPlate`** — the visible dark bar.
- **`slider/[BuildingBlock] Cube (2)`** — the slider knob. `Grabbable` + `OneGrabTranslateTransformer` (X-only, relative constraints) + `Rigidbody` (kinematic, gravity off, Y/Z position + all rotation frozen). Only the transformer can move it, and only along X.
- **`slider/FillBar`** — colored fill strip driven by `PlateFillPercent.fillBar`.

## 6. Physics rules currently in place

- `Player` layer collides with everything (including `Cubes`).
- `CharacterController.stepOffset = 0.2` — short cubes are climbable, taller props are walls.
- Slider knob's Rigidbody has `FreezePositionY | FreezePositionZ | FreezeRotation*` so it can't fall, tip, or slide off the track.
- Cube / dumbbell Rigidbodies use `Interpolate` + `Continuous` collision detection.

## 7. Common tuning (Inspector only, no code changes)

| Object | Component | Field | Effect |
|--------|-----------|-------|--------|
| `Player` | `CompleteVRLocomotion` | `physicalMoveGain` | Ratio between real steps and in-game steps. 1 = 1:1, 3 = each real step covers 3× (current default). |
| `Player` | `CompleteVRLocomotion` | `swingSensitivity`, `swingInputSmoothing`, `swingRampTime`, `minSwing` | Arm-swing running feel |
| `Player` | `CompleteVRLocomotion` | `recenterEyeHeight` | Y position (m) the head should report after pressing Y |
| `Player` | `playerPushScript` | `pushPower` | Strength of the nudge when walking into a cube |
| `Dumbbell` | `DumbbellWeight` | `minMassKg` / `maxMassKg` | Physical weight range in kg |
| Slider knob | `PlateFillPercent` | `fillColorLow` / `fillColorHigh` | Fill bar color ramp |

## 8. Common issues

- **"Grab doesn't hold"** — object needs a `Grabbable`, a `GrabInteractable`, a `Collider` sized to the visible mesh, **and** an `IGrabTransformer` (`GrabFreeTransformer` is the usual pick). Missing the transformer is the silent-failure mode — the grip selects it but nothing moves.
- **"Player spawned too tall / floating"** — press Y to recenter.
- **"Player barely moves in-world when I walk"** — increase `physicalMoveGain`.
- **"Real hand appears inside the visor"** — bump the local Z on `[BuildingBlock] Hand Tracking left/right` above 0.06 m.
- **"`COM4 unavailable`"** — the ESP32 isn't plugged in. The game still runs; ignore.
- **"Slider is stuck / teleports"** — the knob's Rigidbody constraints must include `FreezePositionY | FreezePositionZ | FreezeRotation*`, and the `OneGrabTranslateTransformer._constraints` must have `ConstraintsAreRelative = true`. Both are set by default in this scene.
