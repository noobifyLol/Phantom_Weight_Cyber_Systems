This is the Phantom Weight project.

## Error Log ##
Errors that we have solved in the pass, the controller not appearing and the controller inputs are not being detected by the meta XR interaction kit. Assets not loading into unity (they appear purple or with max brightness). We also fixed when the hand and controller are connecting at the same time model blend issue where we just did individual states instead of combining them. We also fixed the physical and height move in real life and moving like 3cm by getting the change distance and then scaling with a conversion equation. For this problem "Make the objects smaller because when the person picks up the objects, it looks way to big compared to when the person is far apart (I know that this might be common sense but it's different from our environment which makes the experience less realistic)" by adding grab transformer scripts that constraint size to our grabbable objects. By adding gravity and allowing kinematics when grabbing the blocks we can make the blocks solid but not the person. Also fixed the grey screen issues by disabling tunneling and passthrough scripts. Add box colliders and through adding player controller for the camera and then transforming the camera on the 3d Space, it fixed the box collider and camera clipping into the ground issue.

Things that we have to do right now : 
- Add gravity for the environment and fix the pass through
- Make the calibration screen
  

### Full error log (reconstructed from Git + Plastic version control history) ###
The paragraph above is the short version. Here's the fuller list pulled from commit/changeset history, in roughly chronological order:

- **Git and Plastic tracking each other's internals** — the project already had a live Plastic workspace when Git was set up, so each was staging the other's state files (Plastic was staging 712 items including the 47 MB `.git/lfs` store). Fixed with `ignore.conf` (Plastic) and `.gitignore` (Git) rules so each VCS ignores the other. (7/22)
- **Furniture_ges1 room asset pack blown out to solid white** — the pack was authored for Built-in RP in Gamma space and relied on a bundled Post Processing profile that URP + Linear ignores entirely. Ambient Intensity, Albedo Boost, and Indirect Output Scale were all stuck at inflated values (8 / 3.41 / 1.86) and clipped to white; reset to Unity defaults (1/1/1), plus a background material (`fonas1.mat`) that had the backdrop photo wired in as a full-white emission map. (7/22)
- **Meta XR SDK wouldn't compile on Unity 6000.5.4f1** — `Object.GetInstanceID()` / the `EntityId -> int` implicit conversion became obsolete-as-error (CS0619) in Unity 6.5, and Meta's SDK 203 (`com.meta.xr.sdk.core`, `.interaction`, `.mrutilitykit`) only had this migration half-done. Forked all three packages into `Packages/` and patched every unguarded call site to Meta's own `EntityId.ToULong()` pattern behind `UNITY_6000_5_OR_NEWER`. (7/22)
- **XR Plug-in Management had no loader configured** — installed `com.unity.xr.openxr` 1.17.1 and wired up `Assets/XR/OpenXRLoader.asset` + settings so Android actually has an XR runtime. (7/22)
- **Materials rendering pink/green in the Game view** — `Bricks_Red_Rough-Hewn.mat` (and others) still used the Built-in Standard shader instead of URP Lit; converted with textures/keywords preserved. A Realtime/OnAwake reflection probe was also causing pink ceiling / green wall artifacts — switched to Baked/ViaScripting. (7/27)
- **Android build warning: invalid product name** — `My project (1)` contained characters Android build tooling rejects; renamed to `UMD CSPE`. (7/27)
- **Quest 3 performance** — render scale was 160% (biggest single GPU cost), shadowmap/­distance were oversized for room-scale VR, and there was no `QuestPerformanceSetup.cs` forcing 90 Hz / SustainedHigh / dynamic foveated rendering. All tuned down/added. (7/27)
- **Locomotion could fall through the floor** — `SimpleXRLocomotion` had jump enabled by default with no guaranteed floor colliders; defaulted to off. (7/27)
- **Hand/controller model blend glitch** — when hand tracking and controllers were both active at once, the models fought each other; fixed by treating hand-tracked and controller states independently instead of trying to blend them. Also removed an unrelated black-blur post-process artifact. (7/28)
- **Box colliders and camera clipping into the ground** — adding proper box colliders plus a real player controller (instead of moving the bare camera) fixed the camera sinking into the floor. (7/28)
- **Real-world movement only translated ~3 cm of in-VR movement** — fixed by measuring the real head-position delta each frame and scaling it through a conversion equation ("height responsive" movement). (7/28)
- **A bad Plastic pull deleted the Meta SDK core/interaction packages and box-collider/player-controller work** — recovered from history (changeset 54) across several restore commits; the interaction package restore was initially incomplete and left `CollisionInteractionRegistry`/`InteractorGroup` CS0246 errors, fixed by diffing the restored tree against history and pulling the missing files. (7/29)
- **Grey screen on headset** — caused by tunneling/passthrough scripts; disabled as part of the "Major VR HeadSet change" pass that also solidified box colliders and improved locomotion. (7/30)
- **Objects looked oversized when grabbed close vs. viewed from a distance** — added grab transformer scripts that constrain the scale of grabbable objects so proportions stay believable at grab range. (documented in the short error log above)
- **`Assembly-CSharp` duplicate-key build error + missing `MetaQuestFeature` asset** — caused by an embedded Meta SDK version (203) fighting with a Package-Manager-resolved version (205) at the same time. Fixed by removing the direct 203 package entries so Unity resolves a single consistent 205 from the registry. (8/3-8/4)
- **`GrabDetector.cs` referenced Interaction SDK APIs that don't exist** (`Grabbable.SelectingInteractors`, `IInteractorView.Transform`) — `Grabbable` only exposes grab-point poses (`SelectingPoints`), not per-hand identity. Rewritten to identify the grabbing hand by proximity to `OVRCameraRig.leftHandAnchor` / `rightHandAnchor` instead. (8/3-8/4)
- **Corrupted package caches** — `com.meta.xr.sdk.audio`'s `MetaXRAcousticMap.cs` referenced Editor-only `UnityEditor.GUID` from Runtime code, and `com.unity.ai.navigation`'s cached copy was missing most of its `Runtime/*.cs` files and silently failed to compile. Both embedded directly into `Packages/` to force a clean import. (8/4)
- **Android build target misconfigured** — architecture was set to `x86_64` (Quest needs ARM64) and the build's scene list still pointed at the empty default `SampleScene` instead of `UMD CSPE.unity`. Fixed both so on-device builds actually contain the game. (8/4)
- **`NullReferenceException` in Meta's `FirstPersonLocomotor` (`GetModifiedSpeedFactor`), spamming every frame** — a leftover Meta Building-Block component (`ControllerSlideInteractor`'s `SlideLocomotionBroadcaster`, part of the default thumbstick "slide" locomotion) was still broadcasting locomotion events every `Update()` into a `FirstPersonLocomotor` that we don't use (our real locomotion is the custom `CompleteVRLocomotion` script) and that had never been initialized. Disabling just the receiving component wasn't enough since the broadcaster called its public `HandleLocomotionEvent` directly, bypassing the disabled check, and outright deleting the component broke a `LocomotionEventsConnection`'s serialized `Handlers` list elsewhere (`Invalid item in the collection Handlers at index 0`). Fixed at the source: disabled `SlideLocomotionBroadcaster` so no events are generated in the first place, keeping `FirstPersonLocomotor` in place (just inert). (8/4)
- **`PlateFillPercent.cs` weight-plate percentage capped at 50% instead of 100%** — `Mathf.InverseLerp` already returns 0-1, but the script multiplied by `50f` instead of `100f`, so sliding the plate all the way to its "100%" end only ever displayed/reported 50. Fixed to `* 100f`. (8/4)

## Personal Weight Formula ##
<img width="461" height="824" alt="image" src="https://github.com/user-attachments/assets/ab58254c-f175-49d1-befd-b85038623c12" />



## Hardware coding framework ##
1. Player picks up item -> Prints out weight assigned to item -> Converts weight to # of button presses through formula in a script
2. Send # of number of button presses to ESP32 -> through terminal -> EMS how many times to write to OP
3. When player releases item -> check if grabbing or not -> if not then loop all the way to 0 (need global currentWeight variable which solves dropping and picking up really fast)


## Starting out in Unity ##
1. ** The first part of the software side of this project is the unity environment **
In the unity environment, you can download the official unity installer at their website. For the environment in this project, you can make your own room or import a room from unity assets. Then make sure that you install Meta all in one SDK into you unity project so you get the meta Building blocks. 

2. First drag the camera rig into your environment.

3. Connect the camera and the controllers into the environments

4. Add the Loco move script to the camera rig so the player can move and control the player in the game using controllers

5. Add cubes or imported assets and then add the meta block grabbable script to it.

6. Add the [BuildingBlock] OVRComprehensiveInteractionRig to bring back the hand models and controller models together (this handles the blend between hand tracking and controller models automatically instead of us hand-swapping them).

7. On each grabbable block, add the `CubeGrabDetector` script (`Assets/Scripts/GrabDetector.cs`) alongside the `Grabbable` component so it can detect grabs and talk to the ESP32 over serial (see "Software -> Hardware bridge" below).

8. For blocks that should feel soft/give a little when grabbed, assign the `softMaterial` Physic Material (`Assets/physicsMaterials/softMaterial.physicMaterial`) to the block's Collider.


## Software -> Hardware bridge ##
This is how a grab in the headset turns into a physical signal on the ESP32.

1. `Assets/Scripts/GrabDetector.cs` (`CubeGrabDetector`) sits on each grabbable block. Every frame it reads `Grabbable.SelectingPoints` (the active grab points on that object) and figures out which hand each grab point belongs to by checking which is closer: `OVRCameraRig.leftHandAnchor` or `rightHandAnchor`. It supports both hands grabbing independently at the same time.
2. When a hand starts grabbing, it sends `"Lift,{blockWeight},{hand}"` over the serial port (default `COM4`, 115200 baud) to the ESP32, e.g. `Lift,30,Left`. When it lets go, it sends `"Release,0,{hand}"`.
3. `Firmware/ESP32/ESP32.ino` is the Arduino sketch that runs on the ESP32. It reads that same `Command,Weight,Hand` line over Serial, splits it on commas, and drives `digitalWrite()` on GPIO 4 (Left channel) or GPIO 2 (Right channel) HIGH on `Lift` / LOW on anything else for that hand. This is what triggers the EMS/resistance hardware referenced in the Hardware coding framework above.
4. The serial connection only opens in the Editor / Windows standalone builds (guarded by `#if UNITY_EDITOR || UNITY_STANDALONE_WIN`) since `System.IO.Ports.SerialPort` isn't available on the Quest/Android build target — on-device builds simply skip the ESP32 calls so the app doesn't crash when no serial device is present.


## Other gameplay scripts ##
- `Assets/Scripts/PlateFillPercent.cs` — drives the weight-plate UI, converting the plate's local X position into a 0-100% fill label (`TMP_Text`) that floats above the plate.
- `Assets/Scripts/Billboard.cs` — makes a UI/label object always face the camera by matching its rotation every frame (used for the floating percent labels and similar world-space text).
- `Assets/Scripts/PokeLogger.cs` — small debug helper wired to the Meta SDK's poke/click event so pokes on a button show up in the console log.


## Features added (reconstructed from Git + Plastic version control history) ##
- **Git + Git LFS** set up for the project alongside the existing Plastic workspace, with textures/models/audio/binaries routed through LFS and Unity YAML files kept as mergeable text. (7/22)
- **Meta Building Blocks XR rig** added to the main scene — Camera Rig, real hand visuals, hand tracking (left/right), controller tracking (left/right) with Touch controller models, and the full OVR/OpenXR hand skeleton hierarchy. (7/23)
- **VR locomotion** (`Assets/Scripts/SimpleXRLocomotion.cs`) — left-thumbstick smooth move, right-thumbstick snap turn, plus a "Physical Move Gain" option that amplifies real headset movement so a small real step covers more ground (compensates for the room being scaled larger than our real play space). (7/23)
- **Grab + haptics building blocks** — `[BuildingBlock] Cube` objects with Rigidbody + Grabbable, `[BuildingBlock] Haptics`, and the Hand Grab installation routines. Cinemachine package added for camera work. (7/23)
- **Visual realism pass for Quest 3** — ACES tonemapping, bloom, punchier color adjustments, screen-space ambient occlusion (grounds furniture / adds contact shadows), and higher-resolution/longer-distance/soft shadows. (7/27)
- **`QuestPerformanceSetup.cs`** — attaches next to `OVRManager` to force 90 Hz display refresh, SustainedHigh CPU/GPU perf levels, and dynamic foveated rendering (level 3) on Quest 3. (7/27)
- **Height-responsive movement** — real-world height/position changes are tracked and scaled into the VR camera rig. (7/28)
- **Dial knob interaction.** (7/31)
- **Personal Weight Formula** designed (diagram in the section above) and the **Hardware coding framework** for turning a grabbed item's assigned weight into ESP32 button-press/EMS output. (7/29)
- **ESP32 firmware** (`Firmware/ESP32/ESP32.ino`) — parses a `Command,Weight,Hand` line over Serial and drives GPIO 4 (Left) / GPIO 2 (Right) high on `Lift`, low otherwise.
- **`GrabDetector.cs` (`CubeGrabDetector`)** — per-object script that detects which hand is grabbing (via proximity to the camera rig's hand anchors, supports both hands independently) and sends the matching `Lift`/`Release` command to the ESP32 over serial.
- **`[BuildingBlock] OVRComprehensiveInteractionRig`** added back to the scene so hand models and controller models both appear/blend correctly again.
- **Soft-body physics material** (`Assets/physicsMaterials/softMaterial.physicMaterial`) for grabbable blocks that should give a little rather than feel perfectly rigid. (8/4)
- **Weight-plate fill UI** — `PlateFillPercent.cs` converts a plate's position into a 0-100% label, kept facing the player by `Billboard.cs`.
- **`PokeLogger.cs`** — debug helper that logs to the console when a Meta SDK poke/click event fires on a button.
- **Meta XR SDK realigned to a single consistent Package Manager version (205)**, with `com.meta.xr.sdk.audio` and `com.unity.ai.navigation` embedded locally to fix packages whose cached copies had import/compile problems.
- **`WeightPreviewCube` + `WeightScaleCube.cs`** — a new cube (separate from the three existing `[BuildingBlock] Cube` objects, which are untouched) that visualizes the "invisible weight" illusion: it reads the calibration plate's 0-100 `PlateFillPercent.FillPercent` value and scales itself from 0.3x to 2.5x accordingly, so sliding the plate toward a heavier EMS setting makes the cube visibly look heavier. (8/4)

## Version control ##
Day-to-day work is tracked in **Unity Version Control (Plastic SCM)** (`Phantom Weight/UMD CSPE`, branch `main`), which several teammates check into directly from the Unity Editor's Plastic panel or the `cm` CLI. There is also a separate GitHub mirror (`Phantom_Weight_Cyber_Systems`, branch `main-temp`) used for pull requests — the two aren't automatically kept in sync, so check both if something looks out of date.

