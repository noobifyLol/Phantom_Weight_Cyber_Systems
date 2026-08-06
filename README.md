This is the Phantom Weight project.


## Things that we have to do right now ##

- Fix the CompleteLocoMotion Script for the B button
- Fix block acceleration and block flying collosion with the player
- Connect ESP32 wirelessly *(a Bluetooth rewrite of the firmware already exists at `Assets/Scripts/main.cpp.txt` — uses `BluetoothSerial` instead of wired USB — but hasn't been moved into `Firmware/ESP32/` as the canonical version yet)*


## Personal Weight Formula ##
<img width="461" height="824" alt="image" src="https://github.com/user-attachments/assets/ab58254c-f175-49d1-befd-b85038623c12" />


## Error Log ##
Errors that we have solved in the past: the controller not appearing and controller inputs not being detected by the Meta XR interaction kit; assets not loading into Unity (appearing purple or at max brightness); the hand-and-controller-connected-simultaneously model blend glitch (fixed by treating hand-tracked and controller states independently instead of trying to blend them); real-life physical movement only translating ~3cm of in-VR movement (fixed by measuring the real head-position delta and scaling it through a conversion equation); objects looking way too big when picked up compared to viewing them from a distance (fixed with grab transformer scripts that constrain scale); making blocks solid via gravity + kinematic-while-grabbed instead of making the *person* solid; the grey-screen issue (disabled tunneling/passthrough scripts); box colliders and camera clipping into the ground (fixed by adding real box colliders and a proper player controller instead of moving the bare camera). OpenXR or MetaQuest isn't running the unity environment when the computer clicked play. This was fixed by deleting corrupted cache files of com.unity.xr.openxr and then changing the openxr setting in unity to android to using openxr. 

**Block grabbing (8/5):** only the slider could be picked up with the controller trigger, not the actual grabbable cubes. Cause: the two cubes' `Rigidbody.excludeLayers` was set to the `Player` layer, and the whole hand/controller rig lives on that same `Player` layer. Meta's grab system finds candidates through actual Unity trigger events (`OnTriggerEnter`), so excluding the Player layer silently meant the cubes could never trigger-overlap the hand and never got added to the grab candidate list — even though the Physics Layer Collision Matrix looked completely fine. Fixed by clearing `excludeLayers` back to nothing on both cubes. Also attached `playerPushScript` (already written, just never attached) to the Player so walking into a cube gently pushes it, scoped to the `Cubes` layer only.

**Height gain / crouch not responsive enough (8/5):** physically crouching in real life barely changed in-game height. `CompleteVRLocomotion` already had "Physical Move Gain" to amplify real *horizontal* steps, but nothing amplified real *vertical* head movement — it was 1:1. Added a matching `heightGain` field: it reads the raw, un-amplified headset height (the same signal the collider-sync code already used) and folds the amplified vertical delta into the tracking-space base height each frame, stacking on top of (not replacing) the existing stick-crouch.

**Follow-up pass (8/6) — height gain inverted, A-button flew, hand-tracking too twitchy, player kept climbing cubes.**
- *Standing up made the view drop.* `HandleCrouch`'s new height-gain block folded the amplified delta in with `_trackingSpaceBaseLocalY -= verticalDelta * (gain-1)`. That cancels the real motion instead of amplifying it — real head Y goes up, tracking-space Y goes down by the same amount, so the head world Y stays put and it *feels* like you shrank. Correct sign is `+=`; now standing up moves the view further up and crouching further down, both scaled by `heightGain`, still fading to 1× near the floor so reaching for ground objects stays precise.
- *A-button "fly".* `HandleGravityAndJump` set `_currentVerticalSpeed = jumpVelocity` any frame `CharacterController.isGrounded` was true — and `isGrounded` briefly re-fires off cube contacts, so holding A stacked jump impulses. Per request, removed jump entirely and renamed the method `HandleGravity`. If jump comes back later it needs a real ground check (a downward SphereCast onto a Floor layer), not `CC.isGrounded`.
- *Bare hand-tracking triggered running from any gesture.* When `OVRInput.GetActiveController()` reports `Hands` there's no grip button to gate on, so the run gate was swing-speed only, and normal hand motion crossed `minSwing = 0.6`. Added a separate `minSwingHands` threshold (default `2.0` m/s) used only in that mode — controllers still gate on both grips + `minSwing`.
- *Player climbing on top of cubes.* Old behaviour let small cubes be step-up-able (`stepOffset = 0.2`), which turned out to be a bug source, not a feature. Dropped default to `0.05` in `playerPushScript` — cubes now act as walls; raise the field back up if you ever want them climbable again.
- *`DumbbellWeight.cs` deleted.* Nothing in the current gameplay loop was using it. If the `Dumbbell` GameObject still shows a `Missing (Mono Script)` row in the Inspector after this update, remove it via the three-dot menu on that row.
- *Do not put a `MeshCollider` on the Dumbbell.* MeshColliders on Rigidbodies must have `Convex = true`; a non-convex one silently makes Unity skip collision on it, so the dumbbell falls through the floor and grabs stop landing. Keep the `BoxCollider`.

### Full error log (reconstructed from Git + Plastic version control history) ###
- **Git and Plastic tracking each other's internals** — the project already had a live Plastic workspace when Git was set up, so each was staging the other's state files (Plastic was staging 712 items including the 47 MB `.git/lfs` store). Fixed with `ignore.conf` (Plastic) and `.gitignore` (Git) rules so each VCS ignores the other. (7/22)
- **Furniture_ges1 room asset pack blown out to solid white** — authored for Built-in RP in Gamma space, relied on a Post Processing profile that URP + Linear ignores entirely. Ambient Intensity, Albedo Boost, and Indirect Output Scale were stuck at inflated values (8 / 3.41 / 1.86); reset to Unity defaults (1/1/1), plus a background material (`fonas1.mat`) that had the backdrop photo wired in as a full-white emission map. (7/22)
- **Meta XR SDK wouldn't compile on Unity 6000.5.4f1** — `Object.GetInstanceID()` / the `EntityId -> int` implicit conversion became obsolete-as-error (CS0619), and Meta's SDK 203 only had the migration half-done. Forked `com.meta.xr.sdk.core`/`.interaction`/`.mrutilitykit` into `Packages/` and patched every unguarded call site behind `UNITY_6000_5_OR_NEWER`. (7/22)
- **XR Plug-in Management had no loader configured** — installed `com.unity.xr.openxr` 1.17.1 and wired up `Assets/XR/OpenXRLoader.asset` + settings. (7/22)
- **Materials rendering pink/green in the Game view** — `Bricks_Red_Rough-Hewn.mat` still used the Built-in Standard shader instead of URP Lit; converted with textures/keywords preserved. A Realtime/OnAwake reflection probe was also causing pink ceiling/green wall artifacts — switched to Baked/ViaScripting. (7/27)
- **Android build warning: invalid product name** — `My project (1)` contained characters Android build tooling rejects; renamed to `UMD CSPE`. (7/27)
- **Quest 3 performance** — render scale was 160% (biggest single GPU cost), shadowmap/distance oversized for room-scale VR, no `QuestPerformanceSetup.cs` forcing 90Hz/SustainedHigh/dynamic foveated rendering. All tuned down/added. (7/27)
- **Locomotion could fall through the floor** — jump was enabled by default with no guaranteed floor colliders; defaulted to off. (7/27)
- **A bad Plastic pull deleted the Meta SDK core/interaction packages and box-collider/player-controller work** — recovered from history (changeset 54); the interaction package restore was initially incomplete and left `CollisionInteractionRegistry`/`InteractorGroup` CS0246 errors, fixed by diffing the restored tree against history. (7/29)
- **`Assembly-CSharp` duplicate-key build error + missing `MetaQuestFeature` asset** — an embedded Meta SDK version (203) fighting with a Package-Manager-resolved version (205) at the same time. Fixed by removing the direct 203 package entries so Unity resolves a single consistent 205. (8/3-8/4)
- **`GrabDetector.cs` referenced Interaction SDK APIs that don't exist** (`Grabbable.SelectingInteractors`, `IInteractorView.Transform`) — `Grabbable` only exposes grab-point poses, not per-hand identity. Rewritten to identify the grabbing hand by proximity to `OVRCameraRig.leftHandAnchor`/`rightHandAnchor`, then later rewritten again to be event-driven (see Features below). (8/3-8/4)
- **Corrupted package caches** — `com.meta.xr.sdk.audio`'s `MetaXRAcousticMap.cs` referenced Editor-only `UnityEditor.GUID` from Runtime code, and `com.unity.ai.navigation`'s cache was missing most of its `Runtime/*.cs` files. Both embedded directly into `Packages/` to force a clean import. (8/4)
- **Android build target misconfigured** — architecture was `x86_64` (Quest needs ARM64), build scene list pointed at the empty default `SampleScene`. Fixed both. (8/4)
- **`NullReferenceException` in Meta's `FirstPersonLocomotor`, spamming every frame** — a leftover Building-Block component (`SlideLocomotionBroadcaster`) was broadcasting locomotion events into a `FirstPersonLocomotor` we don't use (real locomotion is `CompleteVRLocomotion`) and that was never initialized. Fixed at the source by disabling the broadcaster so no events are generated in the first place. (8/4)
- **`PlateFillPercent.cs` weight-plate percentage capped at 50% instead of 100%** — multiplied by `50f` instead of `100f`. Fixed. (8/4)
- **Block grabbing / height gain** — see the two entries above. (8/5)

<<<<<<< Updated upstream
=======
## Things that we have to do right now ##
- Make the calibration screen
- Add gravity for the environment and fix the pass through
- Make the map solid and the blocks solid
- Connect ESP32 wirelessly *(a Bluetooth rewrite of the firmware already exists at `Assets/Scripts/main.cpp.txt` — uses `BluetoothSerial` instead of wired USB — but hasn't been moved into `Firmware/ESP32/` as the canonical version yet)*
- Hand-tracked pinch grabs: cubes have `HandGrabInstallationRoutine` for pose grabs but the dumbbell doesn't — add one or verify pinch grabs on it are landing before ship

## Personal Weight Formula ##
<img width="461" height="824" alt="image" src="https://github.com/user-attachments/assets/ab58254c-f175-49d1-befd-b85038623c12" />
>>>>>>> Stashed changes

## Hardware coding framework ##
1. Player picks up item -> prints out the weight assigned to the item -> converts weight to # of button presses through a formula in a script.
2. Send # of button presses to the ESP32 over serial -> EMS writes that many times.
3. When the player releases the item -> check if still grabbing -> if not, loop back down to 0 (needs a global `currentWeight` variable so rapid drop/re-grab doesn't lose state).

## Starting out in Unity ##
1. **The first part of the software side of this project is the Unity environment.** Download the official Unity installer from their website. Build your own room or import one from the Unity Asset Store, then install the Meta All-In-One SDK so you get the Meta Building Blocks.
2. Drag the camera rig into your environment.
3. Connect the camera and the controllers into the environment.
4. Add the locomotion script (`CompleteVRLocomotion.cs`) to the camera rig so the player can move using controllers.
5. Add cubes or imported assets and add the Meta Grabbable block to them.
6. Add `[BuildingBlock] OVRComprehensiveInteractionRig` to bring back the hand models and controller models together (handles the blend between hand tracking and controller models automatically).
7. On each grabbable block, add the `GrabDetector` script alongside the `Grabbable` component so it can detect grabs and talk to the ESP32.
8. For blocks that should feel soft/give a little when grabbed, assign the `softMaterial` Physic Material (`Assets/physicsMaterials/softMaterial.physicMaterial`) to the block's Collider.

## Software -> Hardware bridge ##
1. `Assets/Scripts/GrabDetector.cs` sits on each grabbable object (`RequireComponent(Grabbable)`). It's event-driven — subscribed to `Grabbable.WhenPointerEventRaised` — rather than polling every frame, so a grab-and-release inside a single frame can't be missed and four objects aren't all polling `Update()` needlessly. It figures out which hand grabbed by proximity of the grab-point pose to `OVRCameraRig.leftHandAnchor`/`rightHandAnchor`.
2. On `Select`, it sends `"Lift,{weight},{hand}"` (weight read live from `PlateFillPercent.percent` at the moment of the grab) via `Esp32Bridge.Send(...)`, e.g. `Lift,30,Left`. On `Unselect`/`Cancel`, it sends `"Release,{weight},{hand}"` using that **same** weight it grabbed with — not a fixed 0, since the ESP32's release pulse count needs to match what it pulsed up by.
3. `Esp32Bridge.cs` is a static, process-wide owner of the serial port — previously every `GrabDetector` opened its own `SerialPort` on `COM4`, so four grabbable objects meant four competing "port busy" warnings. Now any script just calls `Esp32Bridge.Send(payload)` and doesn't care whether the port is open, missing, or unsupported on the current build target.
4. `Firmware/ESP32/ESP32.ino` is the Arduino sketch running on the ESP32 (wired USB). It reads the `Command,Weight,Hand` line over Serial. Left hand (GPIO 4) is a simple `digitalWrite` HIGH on `lift` / LOW on `release`. Right hand pulses: GPIO 2 HIGH/LOW `weight` times on `lift` (pulse UP), GPIO 5 HIGH/LOW `weight` times on `release` (pulse DOWN) — each pulse ~67ms. (A separate Bluetooth-based rewrite exists at `Assets/Scripts/main.cpp.txt` — see the TODO list.)
5. The serial connection only opens in the Editor / Windows standalone builds (`#if UNITY_EDITOR || UNITY_STANDALONE_WIN`) since `System.IO.Ports.SerialPort` isn't available on Android/Quest — on-device builds just skip the ESP32 calls.

## Other gameplay scripts ##
- `PlateFillPercent.cs` — reads the calibration slider's world X position and turns it into a 0-100 `percent` value that `GrabDetector` reads (weight sent to the ESP32). Also drives the slider's visual polish: a floating `"NN%"` label and a coloured fill bar along the track that grows and tints low→high so the slider reads as an actual gauge instead of a flat plate.
- `playerPushScript.cs` — sits on the Player. Gently pushes cubes on the `Cubes` layer when the player's capsule bumps into them while walking (small impulse, damped so it doesn't ping-pong), and sets a moderate `CharacterController.stepOffset` so low cubes are climbable but taller props aren't.
- `Billboard.cs` — makes a UI/label object always face the camera by matching its rotation every frame.
- `PokeLogger.cs` — debug helper wired to the Meta SDK's poke/click event; logs the poke and disables a `DisclaimerCanvas` if one is present in the scene (calibration-screen scaffolding).

## Features added (reconstructed from Git + Plastic version control history) ##
- **Git + Git LFS** set up alongside the existing Plastic workspace, with textures/models/audio/binaries routed through LFS and Unity YAML kept as mergeable text. (7/22)
- **Meta Building Blocks XR rig** — Camera Rig, real hand visuals, hand tracking (L/R), controller tracking (L/R) with Touch controller models, full OVR/OpenXR hand skeleton hierarchy. (7/23)
- **Grab + haptics building blocks** — `[BuildingBlock] Cube` objects with Rigidbody + Grabbable, `[BuildingBlock] Haptics`, Hand Grab installation routines. Cinemachine added for camera work. (7/23)
- **Visual realism pass for Quest 3** — ACES tonemapping, bloom, punchier color adjustments, screen-space ambient occlusion, higher-res/longer-distance/soft shadows. (7/27)
- **`QuestPerformanceSetup.cs`** — 90Hz display refresh, SustainedHigh CPU/GPU, dynamic foveated rendering level 3. (7/27)
- **`CompleteVRLocomotion.cs`** — the current, single locomotion script (superseded an earlier `SimpleXRLocomotion.cs`). Combines: joystick move; two-hand arm-swing running (double-exponential-smoothed so it isn't choppy; requires *both* grips on controllers so a one-handed grab reach doesn't accidentally trigger a run; on bare hand tracking, gates on a separate, higher swing threshold `minSwingHands` since there's no grip button); gravity; smooth or snap turn; stick-crouch; **Physical Move Gain** (amplifies real horizontal steps); **Physical Height Gain** (same idea, vertically — added 8/5, fixed sign 8/6); and a recenter button (default Y on the left controller) that snaps the tracking space back to a configured eye height. Jump was removed on 8/6 — the A-button "fly" bug came from stacked jump impulses off cube contacts.
- **`GrabDetector.cs` / `Esp32Bridge.cs` / `playerPushScript.cs`** — see "Other gameplay scripts" and "Software -> Hardware bridge" above.
- **Soft-body physics material** (`Assets/physicsMaterials/softMaterial.physicMaterial`) for grabbable blocks that should give a little rather than feel perfectly rigid. (8/4)
- **Meta XR SDK realigned to a single consistent Package Manager version (205)**, with `com.meta.xr.sdk.audio` and `com.unity.ai.navigation` embedded locally to fix packages whose cached copies had import/compile problems. (8/4)
- **`[BuildingBlock] OVRComprehensiveInteractionRig`** added back so hand models and controller models both appear/blend correctly. (8/4)

## Version control ##
Day-to-day work is tracked in **Unity Version Control (Plastic SCM)** (`Phantom Weight/UMD CSPE`, branch `main`), which several teammates check into directly from the Unity Editor's Plastic panel or the `cm` CLI. There is also a separate GitHub mirror (`Phantom_Weight_Cyber_Systems`, branch `main-temp`) used for pull requests — the two aren't automatically kept in sync, so check both if something looks out of date.
