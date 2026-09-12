# Phantom Weight — VR Haptic Wearable Project

---

## 🔧 Immediate To-Dos (Don't read this Part)

- [ ] **Connect ESP32 wirelessly** — a Bluetooth rewrite of the firmware already exists at `Assets/Scripts/main.cpp.txt` (uses `BluetoothSerial` instead of wired USB), but it hasn't been moved into `Firmware/ESP32/` as the canonical version yet.
- [ ] **Add a body** — so when someone looks down, they see a body. (`FeelFollower.cs` was started for this — see the open issue in the Error Log.)
- [ ] **Fix grabbing** — resolve object transforming/dilating issues.
- [ ] **Document grabbable-object setup and ESP32 reaction behavior** — write up how to make things grabbable and how the ESP32 should react.
- [ ] **Re-add scene objects lost in the 9/12 merge if still wanted** — `Dumbbell` prefab, `Garage` prefab, `Directional Light` (see Error Log → 9/12).

---

## ⚖️ Personal Weight Formula

<img width="461" height="824" alt="Personal Weight Formula" src="https://github.com/user-attachments/assets/ab58254c-f175-49d1-befd-b85038623c12" />

---

## 🚀 Getting Started in Unity

1. **Set up Unity.** Install **Unity 6000.5.4f1** (the version in `ProjectSettings/ProjectVersion.txt`). Build your own room or import one from the Unity Asset Store, then install the Meta All-In-One SDK to get access to Meta Building Blocks.
2. Drag the camera rig into your environment.
3. Connect the camera and controllers into the environment.
4. Add the locomotion script (`CompleteVRLocomotion.cs`) to the camera rig so the player can move via controllers.
5. Add cubes or imported assets and attach the Meta **Grabbable** block to them.
6. Add `[BuildingBlock] OVRComprehensiveInteractionRig` to bring back hand and controller models together (handles the blend between hand tracking and controller models automatically).
7. On each grabbable block, add the `GrabDetector` script alongside the `Grabbable` component so it can detect grabs and talk to the ESP32.
8. For blocks that should feel soft when grabbed, assign the `softMaterial` Physic Material (`Assets/physicsMaterials/softMaterial.physicMaterial`) to the block's Collider.

> **On campus Wi-Fi?** Log in to the Wi-Fi splash page in a browser *before* opening Unity, or Package Manager can't reach `packages.unity.com` (see Error Log → 8/14–8/16).

---

## 🖐️ Making an Object Grabbable

1. Using Meta BuildingBlock, add `[BuildingBlock] HandGrabInstallationRoutine` to the object you want grabbable.
2. On the object itself, add: `Grab Interactable (Script)`, `Box Collider`, `Grabbable (Script)`, `RigidBody`, `Grab Detector (Script)`, `Grab Free Transformer (Script)`, `Interactable Trigger Broadcaster (Script)`.
3. Inside `[BuildingBlock] HandGrabInstallationRoutine`, add: `Hand Grab Interactable (Script)`, `Grab Interactable (Script)`, `Move Towards Target Provider (Script)`.
4. In the object's inspector, drag `Grab Free Transformer (Script)` into the optionals section of `Grabbable (Script)` (One Grab Transformer and Two Grab Transformer).
5. Inside `[BuildingBlock] HandGrabInstallationRoutine`, drag the object's `Grabbable (Script)` into the pointable element of `Hand Grab Interactable (Script)`, and its `RigidBody` into the RigidBody field.
6. Still inside `[BuildingBlock] HandGrabInstallationRoutine`, drag the object's `Grabbable (Script)` into the pointable element of `Grab Interactable (Script)`, and its `RigidBody` into the RigidBody field.

---

## 🔌 Software → Hardware Bridge

**Data flow:**
1. Player picks up an item → the weight is read from the calibration slider (`PlateFillPercent.percent`) → scaled by the per-object `multiper` slider and clamped to 0–100.
2. `GrabDetector` reports the grab to `Esp32Bridge`, which tracks **both hands** and sends the combined state to the ESP32 over serial → the EMS unit writes that many times.
3. When the player releases the item → `Esp32Bridge` re-evaluates what each hand is still holding and sends the new state, so a rapid drop/re-grab or a hand-to-hand swap doesn't lose state.

**Component breakdown:**

- **`Assets/Scripts/GrabDetector.cs`** — sits on each grabbable object (`RequireComponent(Grabbable)`, `RequireComponent(Collider)`). Event-driven — subscribed to `Grabbable.WhenPointerEventRaised` rather than polling every frame — so a grab-and-release within a single frame can't be missed, and several objects aren't all polling `Update()` needlessly. Determines which hand grabbed by checking proximity of the grab-point pose to `OVRCameraRig.leftHandAnchor`/`rightHandAnchor`.
  - **On `Select`:** turns the collider into a trigger (so a held block can't shove the player), computes `weight = Clamp(Round(percent × multiper), 0, 100)` and calls `Esp32Bridge.SetGrabState(isLeft, true, weight)`.
  - **On `Unselect`/`Cancel`:** makes the collider solid again and calls `Esp32Bridge.SetGrabState(isLeft, false, 0)`.
  - `multiper` (Inspector: *Weight Based on Slider*, range 0–2) lets individual objects feel lighter/heavier than the slider value. Debug getters: `IsLeftHeld()`, `IsRightHeld()`, `AreBothHeld()`.
- **`Assets/Scripts/Esp32Bridge.cs`** — a static, process-wide owner of the serial port (`COM4`, 115200 baud) **and** the grab state manager. Previously every `GrabDetector` opened its own `SerialPort`, causing competing "port busy" warnings; now only the bridge touches the port. Payloads it sends:

  | State | Payload(s) |
  |---|---|
  | Both hands holding | `Lift,{max weight},both` |
  | Left only | `Lift,{weight},left` then `Release,0,right` |
  | Right only | `Lift,{weight},right` then `Release,0,left` |
  | Neither | `Release,0,both` |
  | App quit / Play restart | `Release,0,both` (kill switch), then port closed |

- **`Firmware/ESP32/ESP32.ino`** — the Arduino sketch running on the ESP32 (wired USB). Reads `Command,Weight,Hand` lines over Serial. Make sure it handles the lowercase `left`/`right`/`both` hand values above.
  - Left hand (GPIO 4): simple `digitalWrite` HIGH on `lift` / LOW on `release`.
  - Right hand: pulses GPIO 2 HIGH/LOW `weight` times on `lift` (pulse up), GPIO 5 HIGH/LOW `weight` times on `release` (pulse down); each pulse ~67ms.
  - *(A separate Bluetooth-based rewrite exists at `Assets/Scripts/main.cpp.txt` — see To-Dos.)*
- **Serial connection scope:** only opens in the Editor / Windows standalone builds (`#if UNITY_EDITOR || UNITY_STANDALONE_WIN`), since `System.IO.Ports.SerialPort` isn't available on Android/Quest — on-device builds simply skip the ESP32 calls. If the port is missing, it logs one warning and silently no-ops.

---

## 📜 Other Gameplay Scripts

| Script | Purpose |
|---|---|
| `PlateFillPercent.cs` | Reads the calibration slider's world X position and converts it to a 0–100 `percent` value that `GrabDetector` reads for the weight sent to the ESP32. Also drives the slider's visual polish — a floating `"NN%"` label and a colored fill bar that grows/tints low→high so it reads as an actual gauge. |
| `WeightScaleCube.cs` | Visualizes the "invisible weight" illusion: scales a cube from `minScale` (0.3) to `maxScale` (2.5) as `PlateFillPercent` goes 0→100, so heavier settings visibly look heavier. |
| `FeelFollower.cs` | *(Work in progress — body/avatar.)* Keeps an avatar pinned under the headset on the floor (X/Z from `centerEyeAnchor`, Y from the `CharacterController`) and rotates it with head yaw. Has animator/speed fields for a walk animation that isn't wired up yet. **Class name doesn't match the filename — see Error Log.** |
| `playerPushScript.cs` | Sits on the Player. Gently pushes cubes on the `Cubes` layer when the player's capsule bumps into them (small, damped impulse so it doesn't ping-pong). Sets a low `CharacterController.stepOffset` so cubes act as walls. |
| `Billboard.cs` | Makes a UI/label object always face the camera by matching its rotation every frame. |
| `PokeLogger.cs` | Debug helper wired to the Meta SDK's poke/click event. Logs the poke and disables a `DisclaimerCanvas` if present in the scene (calibration-screen scaffolding). |

---

## ✅ Features Added (Changelog)

- **7/22** — Git + Git LFS set up alongside the existing Plastic workspace; textures/models/audio/binaries routed through LFS, Unity YAML kept as mergeable text.
- **7/23** — Meta Building Blocks XR rig: Camera Rig, real hand visuals, hand tracking (L/R), controller tracking (L/R) with Touch controller models, full OVR/OpenXR hand skeleton hierarchy.
- **7/23** — Grab + haptics building blocks: `[BuildingBlock] Cube` objects with Rigidbody + Grabbable, `[BuildingBlock] Haptics`, Hand Grab installation routines. Cinemachine added for camera work.
- **7/27** — Visual realism pass for Quest 3: ACES tonemapping, bloom, punchier color adjustments, screen-space ambient occlusion, higher-res/longer-distance/soft shadows.
- **7/27** — `QuestPerformanceSetup.cs`: 90Hz display refresh, SustainedHigh CPU/GPU, dynamic foveated rendering level 3.
- **7/28** — Fall-recovery safety net for falling off the map; physical crouch scaling.
- **8/4** — Soft-body physics material (`Assets/physicsMaterials/softMaterial.physicMaterial`) for grabbable blocks that should give a little rather than feel perfectly rigid.
- **8/4** — Meta XR SDK realigned to a single consistent Package Manager version (205); `com.meta.xr.sdk.audio` and `com.unity.ai.navigation` embedded locally to fix packages whose cached copies had import/compile problems.
- **8/4** — `[BuildingBlock] OVRComprehensiveInteractionRig` added back so hand and controller models both appear/blend correctly.
- **8/11–8/13** — Scene dressed with asset packs: Simple Garage, Brick Project Studio apartment props, Street Assets (hydrant, oil drums, cones, barriers), office items, AllSkyFree skyboxes, Mnostva Art FREE_Interiors_2 café set. `Esp32Bridge` became a two-hand grab state manager; `WeightScaleCube.cs` and `FeelFollower.cs` added.
- **`CompleteVRLocomotion.cs`** *(current, single locomotion script — superseded the earlier `SimpleXRLocomotion.cs`)*. Combines:
  - Joystick move
  - Two-hand arm-swing running (double-exponential-smoothed so it isn't choppy; requires *both* grips on controllers so a one-handed grab reach doesn't accidentally trigger a run; on bare hand tracking, gates on a separate, higher swing threshold `minSwingHands` since there's no grip button)
  - Gravity
  - **Jump (A button)** — removed 8/6 because of the A-button "fly" bug, later re-added with a real ground check: a `Physics.CheckSphere` at the bottom of the capsule against `groundLayer` (with the player's own layer masked out, so the sphere can't hit your own controller mid-air). If `groundLayer` isn't set it defaults to `Default`.
  - Smooth or snap turn
  - Stick-crouch
  - **Physical Move Gain** (amplifies real horizontal steps)
  - **Physical Height Gain** (same idea, vertically — added 8/5, sign bug fixed 8/6, rewritten 8/12 to use absolute headset height instead of accumulating per-frame deltas)
  - **Height adjustment buttons** — **B** raises the view by `heightStepUp` (0.5 m), **X** lowers it by `heightStepDown` (0.5 m). Stored in a persistent `_manualHeightOffset` (8/12), replacing the older "recenter to a fixed eye height" buttons.
- **`GrabDetector.cs` / `Esp32Bridge.cs` / `playerPushScript.cs`** — see Software → Hardware Bridge and Other Gameplay Scripts above.

---

## 🐛 Error Log / Troubleshooting

### Early / Undated Fixes
- Controller not appearing, and controller inputs not detected by the Meta XR interaction kit.
- Assets not loading into Unity (appearing purple or at max brightness).
- Hand-and-controller-connected-simultaneously model blend glitch — fixed by treating hand-tracked and controller states independently instead of blending them.
- Real-life physical movement only translating ~3cm of in-VR movement — fixed by measuring the real head-position delta and scaling it through a conversion equation.
- Objects looking too big when picked up compared to viewing from a distance — fixed with grab transformer scripts that constrain scale.
- Blocks made solid via gravity + kinematic-while-grabbed, instead of making the *person* solid.
- Grey-screen issue — fixed by disabling tunneling/passthrough scripts.
- Box colliders and camera clipping into the ground — fixed by adding real box colliders and a proper player controller instead of moving the bare camera.
- OpenXR/MetaQuest not running the Unity environment on Play — fixed by deleting corrupted cache files of `com.unity.xr.openxr`, then changing the OpenXR setting in Unity to Android using OpenXR.
- Cube too bouncy / grabbing not realistic — fixed by increasing angular and linear damping on the cubes.
- Block-flying error — fixed by turning the object into a trigger for its colliders while being grabbed.
- Slow gravity / unintended double-jump — the capsule was landing on its bottom edge after a failed jump, allowing a slow, floaty fall. Fixed by adding separate layers to correctly detect the ground.

### Dated Log
- **7/22** — Git and Plastic were tracking each other's internal files (Plastic was staging 712 items including the 47 MB `.git/lfs` store). Fixed with `ignore.conf` (Plastic) and `.gitignore` (Git) rules so each VCS ignores the other.
- **7/22** — `Furniture_ges1` room asset pack rendered as solid white. Authored for Built-in RP in Gamma space, relying on a Post Processing profile that URP + Linear ignores entirely; Ambient Intensity, Albedo Boost, and Indirect Output Scale were stuck at inflated values (8 / 3.41 / 1.86). Reset to Unity defaults (1/1/1); also found a background material (`fonas1.mat`) with the backdrop photo wired in as a full-white emission map.
- **7/22** — Meta XR SDK wouldn't compile on Unity 6000.5.4f1 (`Object.GetInstanceID()` / the `EntityId → int` implicit conversion became obsolete-as-error, CS0619; Meta's SDK 203 only had the migration half-done). Forked `com.meta.xr.sdk.core`/`.interaction`/`.mrutilitykit` into `Packages/` and patched every unguarded call site behind `UNITY_6000_5_OR_NEWER`.
- **7/22** — XR Plug-in Management had no loader configured. Installed `com.unity.xr.openxr` 1.17.1 and wired up `Assets/XR/OpenXRLoader.asset` + settings.
- **7/27** — Materials rendering pink/green in the Game view. `Bricks_Red_Rough-Hewn.mat` was still using the Built-in Standard shader instead of URP Lit; converted with textures/keywords preserved. A Realtime/OnAwake reflection probe was also causing pink ceiling/green wall artifacts — switched to Baked/ViaScripting.
- **7/27** — Android build warning: invalid product name. `My project (1)` contained characters Android build tooling rejects; renamed to `UMD CSPE`.
- **7/27** — Quest 3 performance issues. Render scale was 160% (biggest single GPU cost); shadowmap/distance oversized for room-scale VR; no `QuestPerformanceSetup.cs` forcing 90Hz/SustainedHigh/dynamic foveated rendering. All tuned down/added.
- **7/27** — Locomotion could fall through the floor. Jump was enabled by default with no guaranteed floor colliders; defaulted to off.
- **7/29** — A bad Plastic pull deleted the Meta SDK core/interaction packages and box-collider/player-controller work. Recovered from history (changeset 54); the interaction package restore was initially incomplete and left `CollisionInteractionRegistry`/`InteractorGroup` CS0246 errors, fixed by diffing the restored tree against history.
- **8/3–8/4** — `Assembly-CSharp` duplicate-key build error + missing `MetaQuestFeature` asset. An embedded Meta SDK version (203) was fighting with a Package-Manager-resolved version (205) at the same time. Fixed by removing the direct 203 package entries so Unity resolves a single consistent 205.
- **8/3–8/4** — `GrabDetector.cs` referenced Interaction SDK APIs that don't exist (`Grabbable.SelectingInteractors`, `IInteractorView.Transform`) — `Grabbable` only exposes grab-point poses, not per-hand identity. Rewritten to identify the grabbing hand by proximity to `OVRCameraRig.leftHandAnchor`/`rightHandAnchor`, then later rewritten again to be event-driven (see Features Added).
- **8/4** — Corrupted package caches. `com.meta.xr.sdk.audio`'s `MetaXRAcousticMap.cs` referenced Editor-only `UnityEditor.GUID` from Runtime code, and `com.unity.ai.navigation`'s cache was missing most of its `Runtime/*.cs` files. Both embedded directly into `Packages/` to force a clean import.
- **8/4** — Android build target misconfigured. Architecture was `x86_64` (Quest needs ARM64); build scene list pointed at the empty default `SampleScene`. Fixed both.
- **8/4** — `NullReferenceException` in Meta's `FirstPersonLocomotor`, spamming every frame. A leftover Building-Block component (`SlideLocomotionBroadcaster`) was broadcasting locomotion events into a `FirstPersonLocomotor` that isn't used (real locomotion is `CompleteVRLocomotion`) and was never initialized. Fixed at the source by disabling the broadcaster so no events are generated in the first place.
- **8/4** — `PlateFillPercent.cs` weight-plate percentage capped at 50% instead of 100% — was multiplied by `50f` instead of `100f`. Fixed.
- **8/5 — Block grabbing.** Only the slider could be picked up with the controller trigger, not the actual grabbable cubes. Cause: the two cubes' `Rigidbody.excludeLayers` was set to the `Player` layer, and the whole hand/controller rig lives on that same `Player` layer. Meta's grab system finds candidates through actual Unity trigger events (`OnTriggerEnter`), so excluding the Player layer silently meant the cubes could never trigger-overlap the hand and never got added to the grab candidate list — even though the Physics Layer Collision Matrix looked completely fine. Fixed by clearing `excludeLayers` back to nothing on both cubes. Also attached `playerPushScript` (already written, never attached) to the Player so walking into a cube gently pushes it, scoped to the `Cubes` layer only.
- **8/5 — Height gain / crouch not responsive enough.** Physically crouching in real life barely changed in-game height. `CompleteVRLocomotion` already had "Physical Move Gain" to amplify real *horizontal* steps, but nothing amplified real *vertical* head movement — it was 1:1. Added a matching `heightGain` field: reads the raw, un-amplified headset height (the same signal the collider-sync code already used) and folds the amplified vertical delta into the tracking-space base height each frame, stacking on top of (not replacing) the existing stick-crouch.
- **8/6 — Follow-up pass** (height gain inverted, A-button flew, hand-tracking too twitchy, player kept climbing cubes):
  - *Standing up made the view drop.* `HandleCrouch`'s new height-gain block folded the amplified delta in with `_trackingSpaceBaseLocalY -= verticalDelta * (gain-1)` — that cancels the real motion instead of amplifying it (real head Y goes up, tracking-space Y goes down by the same amount, so head world Y stays put and it *feels* like you shrank). Correct sign is `+=`; standing up now moves the view further up and crouching further down, both scaled by `heightGain`, still fading to 1× near the floor so reaching for ground objects stays precise.
  - *A-button "fly."* `HandleGravityAndJump` set `_currentVerticalSpeed = jumpVelocity` any frame `CharacterController.isGrounded` was true — and `isGrounded` briefly re-fires off cube contacts, so holding A stacked jump impulses. Jump was removed and the method renamed `HandleGravity`. *(Jump has since come back with a real sphere ground check on `groundLayer` — see Features Added.)*
  - *Bare hand-tracking triggered running from any gesture.* When `OVRInput.GetActiveController()` reports `Hands` there's no grip button to gate on, so the run gate was swing-speed only, and normal hand motion crossed `minSwing = 0.6`. Added a separate `minSwingHands` threshold (default `2.0` m/s) used only in that mode — controllers still gate on both grips + `minSwing`.
  - *Player climbing on top of cubes.* Old behavior let small cubes be step-up-able (`stepOffset = 0.2`), which turned out to be a bug source, not a feature. Dropped default to `0.05` in `playerPushScript` — cubes now act as walls; raise the field back up if climbable cubes are ever wanted again.
  - *`DumbbellWeight.cs` deleted.* Nothing in the current gameplay loop was using it. If the `Dumbbell` GameObject still shows a `Missing (Mono Script)` row in the Inspector after this update, remove it via the three-dot menu on that row.
  - *Note: do not put a `MeshCollider` on the Dumbbell.* MeshColliders on Rigidbodies must have `Convex = true`; a non-convex one silently makes Unity skip collision on it, so the dumbbell falls through the floor and grabs stop landing. Keep the `BoxCollider`.
- **8/11–8/12 — Project wouldn't compile: merge conflict markers in scripts.** `Logs/Editor-prev.log` showed `error CS8300: Merge conflict marker encountered` 108× in `CompleteVRLocomotion.cs` and 45× in `FeelFollower.cs`, followed by `error CS0103: The name 'recenterButton' / 'recenterButton2' does not exist in the current context`. A pull had left raw `<<<<<<<` / `=======` / `>>>>>>>` blocks in the files, so Unity refused to compile anything (and every script in the project stops working while any script has a compile error). Fixed by resolving the blocks by hand. **Tip:** after any pull, search the project for `<<<<<<<` before opening Unity.
- **8/12 — CompleteLocoMotion height recentering.** The height adjustment bug occurred because `HandleCrouch()` continuously overwrote the camera's Y position every frame, snapping the view back down shortly after pressing Button B or X. Fixed by converting manual height changes into a permanent offset variable (`_manualHeightOffset`) rather than a temporary single-frame shift — `HandleCrouch()` now applies this persistent offset directly during every frame calculation, keeping view height stable.
- **8/13–8/16 — `Package folder [...\Packages\com.meta.xr.sdk.core] missing package manifest`** (warning, `Logs/upm.log`, ~60×). Leftover from the 7/22 SDK fork: after the 8/4 switch to Package Manager's Meta SDK 205, the folder's `package.json` was removed (7/30, `4635a33`) but `Plugins/Win64OpenXR/OVRPlugin.dll` stayed tracked. Unity just skips the folder. Harmless; to silence it, delete `Packages/com.meta.xr.sdk.core/` (then check Quest Link / Meta XR Simulator still starts in the Editor).
- **8/14–8/16 — Package Manager couldn't reach Unity's servers on campus Wi-Fi.** `Logs/upm.log` filled with `Cannot connect to 'packages.unity.com' (error code: ENOTFOUND/ENOENT)` and `ERR_TLS_CERT_ALTNAME_INVALID: Host: packages.unity.com. is not in the cert's altnames: DNS:wifisplash.sgrove.usmd.edu`. The certificate belonging to the Wi-Fi splash page means the network's captive portal was intercepting all HTTPS traffic because the laptop hadn't logged in to the Wi-Fi yet. Fix: open any website in a browser, finish the Wi-Fi login, then restart Unity (or use another network/hotspot). Packages already in `Library/PackageCache` keep working offline, so the project still opens — you just can't add/update packages.
- **9/12 — Merging diverged `main-temp` branches (30 conflicted files).** A local, never-pushed commit (`e103b9e` "updates", 8/11 — Simple Garage scene, Office Essentials materials, ARCore/OpenXR settings) was merged with the team's newer remote work (8/11–8/22). Conflicts and how they were resolved:
  - **21 materials** (Brick Project Studio, Metal Barrel, Street Assets) — the local side had reset `_Color` to plain white (Unity re-syncing on import), the remote side had the real tints/alpha (e.g. `Metal Barrel Blue 1` blue, glass alpha 0). → Kept **remote**.
  - **`CompleteVRLocomotion.cs` / `GrabDetector.cs`** — local had the old recenter-to-eye-height buttons and a direct `Esp32Bridge.Send(...)`; remote had the 8/12 `_manualHeightOffset` fix and the two-hand `SetGrabState` manager. → Kept **remote** (it's what this README documents).
  - **`Assets/Scenes/UMD CSPE.unity`** (52 conflict blocks) — remote scene has 301 objects vs. 240 locally, and its serialized fields (`heightStepUp`, `recenterButton2`, `multiper`) match the remote scripts. Mixing the two halves of a Unity scene breaks internal `fileID` references, so the whole file was taken from **remote**. Local-only objects **not** carried over: `Dumbbell` prefab instance, `Garage` prefab, `Directional Light`. To recover them, open the old scene from history: `git show 54b7c60:"Assets/Scenes/UMD CSPE.unity" > old.unity` and copy the objects across in the Editor.
  - **`Assets/XR/Settings/OpenXR Package Settings.asset`** — remote enables *Meta XR Subsampled Layout* and *Oculus Touch Controller Proximity* for Android. → Kept **remote**.
  - **`Assets/_Recovery/0.unity`, `0 (1).unity`** — Unity crash-autosave scenes. → Kept **remote**; these can be deleted once nobody needs them.
  - **`ProjectSettings/Packages/com.unity.probuilder/Settings.json`** — editor preferences only. → Kept **local** (superset of the remote keys).
  - **Verified:** no conflict markers left anywhere in the project, and all 29 `Assembly-CSharp` scripts compile with 0 errors (checked offline with Unity 6000.5.4f1's bundled Roslyn compiler).

### Open Issues
- **`FeelFollower.cs` can't be added to a GameObject.** The file is named `FeelFollower.cs` but the class inside is `FeetFollower`. Unity requires a `MonoBehaviour`'s class name to match its filename, so the Inspector reports that the script class can't be found. Fix: rename the file **and** its `.meta` to `FeetFollower.cs` / `FeetFollower.cs.meta` (renaming inside Unity's Project window does both and keeps the GUID), or rename the class to `FeelFollower`.

### Known Harmless Warnings (from `Logs/Editor.log`, 8/19)
These show up every session and don't need fixing:
- `MaterialLocation.External is obsolete` for `AllSkyFree/.../AllSky_DemoEnvironment02.fbx` and `Simple Garage/Models/Garage.fbx` — the asset packs use an old FBX import option. To silence it: select the FBX → **Materials** tab → **Location: Use Embedded Materials** → Apply.
- `Shader 'Oculus/OVRMRCameraFrameLit': fallback shader 'Alpha-Diffuse' not found` and `Shader 'Meta/MRUK/Scene/HighlightsAndShadows': fallback shader 'Off' not found` — inside the Meta SDK; URP doesn't ship those Built-in fallbacks.
- `CinemachineVirtualCameraBaseEditor.cs contains partial class of Unity.Object...` — inside the Cinemachine package.
- `[Licensing::Client] Error: Code 404 ... 0 entitlement groups` and `Project ID request failed ... 401` — the project isn't linked to a Unity Cloud project. Only matters if you use Unity cloud services.
- `NullReferenceException at UnityEditor.SceneViewMotion.SetInputVector` — a Unity Editor bug when the Scene-view WASD fly shortcut fires without a focused Scene view. Not our code.
- `Native extension for Android/WindowsStandalone target not found` and `[Package Manager] ... launched with the -noUpm` (asset import workers) — normal Unity startup noise.

---

## 📈 Project History (from the commit log)

How the project was built, reconstructed from the Git history. Contributors: **noobifyLol** (commits also appear under the name *Noobifiy*), **Mid**, and **datncode**.

### Week 1 — Foundation (7/22–7/23) · *noobifyLol*
- **7/22** — Initialized Git + Git LFS for the Unity project and stopped Git and Unity Version Control (Plastic) from tracking each other.
- **7/22** — Fixed the blown-out room lighting (`fonas1` material, `Furniture_ges1` reset tool, remaining inflated lighting values).
- **7/22** — Safety point, then forked and patched the Meta XR SDK core, interaction and MR Utility Kit so they compile on Unity 6000.5.4f1.
- **7/22** — Enabled XR runtime features, built the room into the main scene, swapped Main Camera for `CameraPlayer`, installed OpenXR and wired the XR loader for Android.
- **7/23** — Added the Meta Building Blocks XR rig, then VR locomotion and grab/haptics blocks to the scene.

### Week 2 — GitHub repo, Quest 3 and ESP32 (7/27–7/30)
- **7/27** *(noobifyLol)* — Initial commit to the GitHub repo `Phantom_Weight_Cyber_Systems`; project overview README; fixed console errors and optimised for the Quest 3 build; **first Unity → ESP32 bridge**; visual-realism pass for Quest 3.
- **7/28** *(Mid)* — Fixed the hand/controller model blend issue and black blur; fixed trigger colliders on map blocks, the jump-at-zero-gravity bug, added physical crouch scaling and a fall-recovery safety net.
- **7/28** *(datncode)* — Fixed the hands-and-controller glitch.
- **7/29** — datncode pushed more fixes; Noobifiy recovered from the bad Plastic pull that deleted SDK packages ("deletion", "unity fix"); Mid merged pull requests #1 and #2 from `main`; datncode documented the **personal weight formula** and hardware framework, Mid added the formula image.
- **7/30** — datncode created the ESP32 firmware (`ESP32.cpp`); Noobifiy pushed a large risky change ("push this with caution"); Mid cleaned up the README and error log.

### Weeks 3–4 — Grabbing, weight and locomotion tuning (8/2–8/7)
- **8/2** — Work moved to the `main-temp` branch (Noobifiy "check" + merge).
- **8/4** — Documentation passes by Noobifiy and datncode (current tasks, issues, corrections); Noobifiy's "finish" commit (SDK 205 realignment, soft physics material, interaction rig, `PlateFillPercent` fix); Mid's follow-up changes.
- **8/5** — Noobifiy: block-grabbing fix (`excludeLayers`), `playerPushScript`, physical height gain.
- **8/6** — datncode improved `setup()`/`loop()` in the ESP32 firmware; Noobifiy's follow-up pass (height-gain sign fix, jump removed, `minSwingHands`, cubes as walls); Mid updated tasks and error log.
- **8/7** — README update by Mid.

### Week 5 — Final build-out (8/11–8/13)
- **8/11** — Noobifiy's local "updates" commit (Simple Garage scene, Office Essentials materials, ARCore/OpenXR settings), merged with Mid's README error resolutions.
- **8/11** — Mid's "Check in" and "ffff": `FeelFollower.cs` (body follower) added, locomotion reworked; this pull is what left the merge conflict markers in the scripts.
- **8/12** — Mid: TODO list and bug-fix documentation, height recentering fix (`_manualHeightOffset`).
- **8/13** — datncode documented grabbable-object setup; Mid's "final push" added the Mnostva Art café interior pack and apartment/street props and tuned materials, plus a final scene tweak.

### Wrap-up (8/22–9/12)
- **8/22** — Mid restructured the README.
- **9/12** — The local and remote `main-temp` branches were merged; all 30 conflicts resolved, project scanned (compile check + Unity log review) and this README updated with the findings and this history.

---

## 🗂️ Version Control

Day-to-day work is tracked in **Unity Version Control (Plastic SCM)** — `Phantom Weight/UMD CSPE`, branch `main` — which several teammates check into directly from the Unity Editor's Plastic panel or the `cm` CLI.

There is also a separate **GitHub mirror** — `Phantom_Weight_Cyber_Systems`, branch `main-temp` — used for pull requests. **The two aren't automatically kept in sync**, so check both if something looks out of date.

**Before opening Unity after a pull:** make sure there are no `<<<<<<<` conflict markers in the project (see Error Log → 8/11–8/12), and push local commits promptly so branches don't drift apart (see Error Log → 9/12).
