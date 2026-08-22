# Phantom Weight — VR Haptic Wearable Project

---

## 🔧 Immediate To-Dos (Don't read this Part)

- [ ] **Connect ESP32 wirelessly** — a Bluetooth rewrite of the firmware already exists at `Assets/Scripts/main.cpp.txt` (uses `BluetoothSerial` instead of wired USB), but it hasn't been moved into `Firmware/ESP32/` as the canonical version yet.
- [ ] **Add a body** — so when someone looks down, they see a body.
- [ ] **Fix grabbing** — resolve object transforming/dilating issues.
- [ ] **Document grabbable-object setup and ESP32 reaction behavior** — write up how to make things grabbable and how the ESP32 should react.

---

## ⚖️ Personal Weight Formula

<img width="461" height="824" alt="Personal Weight Formula" src="https://github.com/user-attachments/assets/ab58254c-f175-49d1-befd-b85038623c12" />

---

## 🚀 Getting Started in Unity

1. **Set up Unity.** Download the official Unity installer. Build your own room or import one from the Unity Asset Store, then install the Meta All-In-One SDK to get access to Meta Building Blocks.
2. Drag the camera rig into your environment.
3. Connect the camera and controllers into the environment.
4. Add the locomotion script (`CompleteVRLocomotion.cs`) to the camera rig so the player can move via controllers.
5. Add cubes or imported assets and attach the Meta **Grabbable** block to them.
6. Add `[BuildingBlock] OVRComprehensiveInteractionRig` to bring back hand and controller models together (handles the blend between hand tracking and controller models automatically).
7. On each grabbable block, add the `GrabDetector` script alongside the `Grabbable` component so it can detect grabs and talk to the ESP32.
8. For blocks that should feel soft when grabbed, assign the `softMaterial` Physic Material (`Assets/physicsMaterials/softMaterial.physicMaterial`) to the block's Collider.

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
1. Player picks up an item → weight assigned to the item is read → converted to a number of button presses via a formula in a script.
2. That number of button presses is sent to the ESP32 over serial → the EMS unit writes that many times.
3. When the player releases the item → check if still grabbing → if not, loop back down to 0. (Needs a global `currentWeight` variable so a rapid drop/re-grab doesn't lose state.)

**Component breakdown:**

- **`Assets/Scripts/GrabDetector.cs`** — sits on each grabbable object (`RequireComponent(Grabbable)`). Event-driven — subscribed to `Grabbable.WhenPointerEventRaised` rather than polling every frame — so a grab-and-release within a single frame can't be missed, and four objects aren't all polling `Update()` needlessly. Determines which hand grabbed by checking proximity of the grab-point pose to `OVRCameraRig.leftHandAnchor`/`rightHandAnchor`.
- **On `Select`:** sends `"Lift,{weight},{hand}"` (weight read live from `PlateFillPercent.percent` at the moment of grab) via `Esp32Bridge.Send(...)` — e.g. `Lift,30,Left`.
- **On `Unselect`/`Cancel`:** sends `"Release,{weight},{hand}"` using the **same** weight it grabbed with (not a fixed 0), since the ESP32's release pulse count needs to match what it pulsed up by.
- **`Esp32Bridge.cs`** — a static, process-wide owner of the serial port. Previously every `GrabDetector` opened its own `SerialPort` on `COM4`, causing four competing "port busy" warnings with four grabbable objects. Now any script just calls `Esp32Bridge.Send(payload)` without needing to know whether the port is open, missing, or unsupported on the current build target.
- **`Firmware/ESP32/ESP32.ino`** — the Arduino sketch running on the ESP32 (wired USB). Reads `Command,Weight,Hand` lines over Serial.
  - Left hand (GPIO 4): simple `digitalWrite` HIGH on `lift` / LOW on `release`.
  - Right hand: pulses GPIO 2 HIGH/LOW `weight` times on `lift` (pulse up), GPIO 5 HIGH/LOW `weight` times on `release` (pulse down); each pulse ~67ms.
  - *(A separate Bluetooth-based rewrite exists at `Assets/Scripts/main.cpp.txt` — see To-Dos.)*
- **Serial connection scope:** only opens in the Editor / Windows standalone builds (`#if UNITY_EDITOR || UNITY_STANDALONE_WIN`), since `System.IO.Ports.SerialPort` isn't available on Android/Quest — on-device builds simply skip the ESP32 calls.

---

## 📜 Other Gameplay Scripts

| Script | Purpose |
|---|---|
| `PlateFillPercent.cs` | Reads the calibration slider's world X position and converts it to a 0–100 `percent` value that `GrabDetector` reads for the weight sent to the ESP32. Also drives the slider's visual polish — a floating `"NN%"` label and a colored fill bar that grows/tints low→high so it reads as an actual gauge. |
| `playerPushScript.cs` | Sits on the Player. Gently pushes cubes on the `Cubes` layer when the player's capsule bumps into them (small, damped impulse so it doesn't ping-pong). Sets a moderate `CharacterController.stepOffset` so low cubes are climbable but taller props aren't. |
| `Billboard.cs` | Makes a UI/label object always face the camera by matching its rotation every frame. |
| `PokeLogger.cs` | Debug helper wired to the Meta SDK's poke/click event. Logs the poke and disables a `DisclaimerCanvas` if present in the scene (calibration-screen scaffolding). |

---

## ✅ Features Added (Changelog)

- **7/22** — Git + Git LFS set up alongside the existing Plastic workspace; textures/models/audio/binaries routed through LFS, Unity YAML kept as mergeable text.
- **7/23** — Meta Building Blocks XR rig: Camera Rig, real hand visuals, hand tracking (L/R), controller tracking (L/R) with Touch controller models, full OVR/OpenXR hand skeleton hierarchy.
- **7/23** — Grab + haptics building blocks: `[BuildingBlock] Cube` objects with Rigidbody + Grabbable, `[BuildingBlock] Haptics`, Hand Grab installation routines. Cinemachine added for camera work.
- **7/27** — Visual realism pass for Quest 3: ACES tonemapping, bloom, punchier color adjustments, screen-space ambient occlusion, higher-res/longer-distance/soft shadows.
- **7/27** — `QuestPerformanceSetup.cs`: 90Hz display refresh, SustainedHigh CPU/GPU, dynamic foveated rendering level 3.
- **8/4** — Soft-body physics material (`Assets/physicsMaterials/softMaterial.physicMaterial`) for grabbable blocks that should give a little rather than feel perfectly rigid.
- **8/4** — Meta XR SDK realigned to a single consistent Package Manager version (205); `com.meta.xr.sdk.audio` and `com.unity.ai.navigation` embedded locally to fix packages whose cached copies had import/compile problems.
- **8/4** — `[BuildingBlock] OVRComprehensiveInteractionRig` added back so hand and controller models both appear/blend correctly.
- **`CompleteVRLocomotion.cs`** *(current, single locomotion script — superseded the earlier `SimpleXRLocomotion.cs`)*. Combines:
  - Joystick move
  - Two-hand arm-swing running (double-exponential-smoothed so it isn't choppy; requires *both* grips on controllers so a one-handed grab reach doesn't accidentally trigger a run; on bare hand tracking, gates on a separate, higher swing threshold `minSwingHands` since there's no grip button)
  - Gravity
  - Smooth or snap turn
  - Stick-crouch
  - **Physical Move Gain** (amplifies real horizontal steps)
  - **Physical Height Gain** (same idea, vertically — added 8/5, sign bug fixed 8/6)
  - Recenter button (default: Y on the left controller) that snaps the tracking space back to a configured eye height
  - *Jump removed 8/6 — the A-button "fly" bug came from stacked jump impulses off cube contacts.*
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
  - *A-button "fly."* `HandleGravityAndJump` set `_currentVerticalSpeed = jumpVelocity` any frame `CharacterController.isGrounded` was true — and `isGrounded` briefly re-fires off cube contacts, so holding A stacked jump impulses. Per request, removed jump entirely and renamed the method `HandleGravity`. If jump comes back later it needs a real ground check (a downward SphereCast onto a Floor layer), not `CC.isGrounded`.
  - *Bare hand-tracking triggered running from any gesture.* When `OVRInput.GetActiveController()` reports `Hands` there's no grip button to gate on, so the run gate was swing-speed only, and normal hand motion crossed `minSwing = 0.6`. Added a separate `minSwingHands` threshold (default `2.0` m/s) used only in that mode — controllers still gate on both grips + `minSwing`.
  - *Player climbing on top of cubes.* Old behavior let small cubes be step-up-able (`stepOffset = 0.2`), which turned out to be a bug source, not a feature. Dropped default to `0.05` in `playerPushScript` — cubes now act as walls; raise the field back up if climbable cubes are ever wanted again.
  - *`DumbbellWeight.cs` deleted.* Nothing in the current gameplay loop was using it. If the `Dumbbell` GameObject still shows a `Missing (Mono Script)` row in the Inspector after this update, remove it via the three-dot menu on that row.
  - *Note: do not put a `MeshCollider` on the Dumbbell.* MeshColliders on Rigidbodies must have `Convex = true`; a non-convex one silently makes Unity skip collision on it, so the dumbbell falls through the floor and grabs stop landing. Keep the `BoxCollider`.
- **8/12 — CompleteLocoMotion height recentering.** The height adjustment bug occurred because `HandleCrouch()` continuously overwrote the camera's Y position every frame, snapping the view back down shortly after pressing Button B or X. Fixed by converting manual height changes into a permanent offset variable (`_manualHeightOffset`) rather than a temporary single-frame shift — `HandleCrouch()` now applies this persistent offset directly during every frame calculation, keeping view height stable.

---

## 🗂️ Version Control

Day-to-day work is tracked in **Unity Version Control (Plastic SCM)** — `Phantom Weight/UMD CSPE`, branch `main` — which several teammates check into directly from the Unity Editor's Plastic panel or the `cm` CLI.

There is also a separate **GitHub mirror** — `Phantom_Weight_Cyber_Systems`, branch `main-temp` — used for pull requests. **The two aren't automatically kept in sync**, so check both if something looks out of date.
