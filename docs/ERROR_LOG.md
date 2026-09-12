<div align="center">

# 🐛 Phantom Weight — Error Log & Troubleshooting

Every bug, build error and warning we've hit, what caused it, and how it was fixed.

[⬅ Back to README](../README.md) · [Quick Fixes](#-quick-fixes) · [Open Issues](#-open-issues) · [Harmless Warnings](#-known-harmless-warnings) · [Dated Log](#-dated-log) · [Early Fixes](#-early--undated-fixes)

</div>

---

## ⚡ Quick Fixes

| Symptom | Likely cause | Fix |
|---|---|---|
| `error CS8300: Merge conflict marker encountered` | A pull/stash left `<<<<<<<` blocks in a script | Search the project for `<<<<<<<`, resolve each block, save. See [8/11–8/12](#dated-log). |
| **Every** script stops working after one change | Any compile error disables all scripts | Fix the first red error in the Console first. |
| ESP32 does nothing when you grab | Wrong COM port, or old firmware that only understood `Left`/`Right` | Set `Esp32Bridge.PortName`, re-flash `Firmware/ESP32/ESP32.ino`. See [9/12 (stash)](#dated-log). |
| `[Esp32Bridge] COMx unavailable — running without EMS` | ESP32 unplugged, wrong port, or Arduino Serial Monitor holding the port | Close Serial Monitor, check Device Manager → Ports, update `PortName`. |
| Can't grab cubes, only the slider | Cube `Rigidbody.excludeLayers` includes `Player` | Clear `excludeLayers`. See [8/5](#dated-log). |
| Grabbable object falls through the floor | Non-convex `MeshCollider` on a moving `Rigidbody` | Tick **Convex** on the MeshCollider (or use a BoxCollider). |
| Package Manager: `Cannot connect to 'packages.unity.com'` / `ERR_TLS_CERT_ALTNAME_INVALID` | Campus Wi-Fi login page intercepting HTTPS | Log in to the Wi-Fi in a browser, restart Unity. See [8/14–8/16](#dated-log). |
| Script can't be added: "class cannot be found" | Filename ≠ class name | Rename file (in Unity, so the `.meta` follows) to match the class. |
| Pink/green/white materials | Built-in shader in URP, or inflated lighting values | Convert to URP Lit; reset lighting. See [7/22](#dated-log), [7/27](#dated-log). |
| Play does nothing on Quest / grey screen | OpenXR cache or passthrough/tunneling scripts | See [Early Fixes](#-early--undated-fixes). |

---

## 🚧 Open Issues

- **Two OpenXR package settings assets.** `Assets/XR/Settings/OpenXRPackageSettings.asset` (added 7/22, listed in `ProjectSettings` → Preloaded Assets) and `Assets/XR/Settings/OpenXR Package Settings.asset` (added 8/11 in "Check in", referenced by `EditorBuildSettings`). They have different feature lists. Unity should only have one — decide which is correct in **Project Settings → XR Plug-in Management → OpenXR**, then delete the other *inside Unity* and re-check the Android build.
- **`Assets/Scripts/main.cpp.txt` is an outdated firmware draft.** Earlier docs called it a Bluetooth rewrite — it isn't. It's an older copy of the wired sketch with an empty left-hand channel and a pin mix-up (pin 4 set up as "pulse down", pin 5 used). `Firmware/ESP32/ESP32.ino` is the only firmware to flash. Wireless (Bluetooth) firmware hasn't been started.
- **Leftover `Packages/com.meta.xr.sdk.core/`** → UPM warning `Package folder [...] missing package manifest`. Only `Plugins/Win64OpenXR/OVRPlugin.dll` remains from the 7/22 fork (its `package.json` was removed 7/30 in `4635a33`). Harmless; delete the folder to silence it, then confirm Quest Link / Meta XR Simulator still start.
- **Firmware blocks while pulsing.** Each right-arm pulse is ~134 ms (`67 ms` HIGH + `67 ms` LOW), so moving 0 → 100 takes ~13 s, during which new serial commands just queue up. A non-blocking (`millis()`-based) pulse loop would fix it.
- **`Assets/_Recovery/0.unity`, `0 (1).unity`** are Unity crash-autosave scenes committed by accident. Safe to delete once nobody needs them.

---

## 🔇 Known Harmless Warnings

From `Logs/Editor.log` (8/19) and the asset import worker logs. These show up every session and don't need fixing:

| Warning | Why it happens | Optional fix |
|---|---|---|
| `MaterialLocation.External is obsolete` (`AllSky_DemoEnvironment02.fbx`, `Simple Garage/Models/Garage.fbx`) | Asset packs use an old FBX import option | Select FBX → **Materials** tab → **Location: Use Embedded Materials** → Apply |
| `Shader 'Oculus/OVRMRCameraFrameLit': fallback shader 'Alpha-Diffuse' not found` / `Shader 'Meta/MRUK/Scene/HighlightsAndShadows': fallback shader 'Off' not found` | Inside the Meta SDK; URP doesn't ship those Built-in fallbacks | — |
| `CinemachineVirtualCameraBaseEditor.cs contains partial class of Unity.Object...` | Inside the Cinemachine package | — |
| `[Licensing::Client] Error: Code 404 ... 0 entitlement groups` / `Project ID request failed ... 401` | Project isn't linked to a Unity Cloud project | Only matters for Unity cloud services |
| `NullReferenceException at UnityEditor.SceneViewMotion.SetInputVector` | Unity Editor bug: WASD fly shortcut without a focused Scene view | — |
| `Native extension for Android/WindowsStandalone target not found`, `[Package Manager] ... -noUpm` | Normal Unity startup / import-worker noise | — |

---

## 📅 Dated Log

### July

- **7/22 — Git and Plastic tracking each other.** Plastic was staging 712 items including the 47 MB `.git/lfs` store. Fixed with `ignore.conf` (Plastic) and `.gitignore` (Git) rules so each VCS ignores the other.
- **7/22 — `Furniture_ges1` room rendered solid white.** Authored for Built-in RP in Gamma space, relying on a Post Processing profile that URP + Linear ignores; Ambient Intensity, Albedo Boost and Indirect Output Scale were stuck at 8 / 3.41 / 1.86. Reset to Unity defaults (1/1/1). Also `fonas1.mat` had the backdrop photo wired in as a full-white emission map.
- **7/22 — Meta XR SDK wouldn't compile on Unity 6000.5.4f1.** `Object.GetInstanceID()` / the `EntityId → int` conversion became obsolete-as-error (CS0619) and SDK 203 was only half migrated. Forked `com.meta.xr.sdk.core` / `.interaction` / `.mrutilitykit` into `Packages/` and patched every call site behind `UNITY_6000_5_OR_NEWER`.
- **7/22 — No XR loader configured.** Installed `com.unity.xr.openxr` 1.17.1 and wired up `Assets/XR/OpenXRLoader.asset` + settings.
- **7/27 — Pink/green materials in the Game view.** `Bricks_Red_Rough-Hewn.mat` still used the Built-in Standard shader — converted to URP Lit with textures/keywords preserved. A Realtime/OnAwake reflection probe caused pink ceiling/green wall artifacts — switched to Baked/ViaScripting.
- **7/27 — Android build warning: invalid product name.** `My project (1)` has characters Android tooling rejects; renamed to `UMD CSPE`.
- **7/27 — Quest 3 performance.** Render scale was 160% (biggest GPU cost); shadowmap/distance oversized for room-scale VR; added `QuestPerformanceSetup.cs` for 90 Hz / SustainedHigh / dynamic foveated rendering.
- **7/27 — Falling through the floor.** Jump was on by default with no guaranteed floor colliders; defaulted to off.
- **7/29 — Bad Plastic pull deleted the Meta SDK core/interaction packages** and box-collider/player-controller work. Recovered from changeset 54; the first restore was incomplete (`CollisionInteractionRegistry` / `InteractorGroup` CS0246), fixed by diffing the restored tree against history.

### August

- **8/3–8/4 — `Assembly-CSharp` duplicate-key build error + missing `MetaQuestFeature`.** Embedded Meta SDK 203 fought with Package Manager's 205. Removed the direct 203 entries so Unity resolves a single 205.
- **8/3–8/4 — `GrabDetector.cs` used Interaction SDK APIs that don't exist** (`Grabbable.SelectingInteractors`, `IInteractorView.Transform`). Rewritten to find the grabbing hand by proximity to `OVRCameraRig.leftHandAnchor` / `rightHandAnchor`, later made event-driven.
- **8/4 — Corrupted package caches.** `com.meta.xr.sdk.audio`'s `MetaXRAcousticMap.cs` used Editor-only `UnityEditor.GUID` from Runtime code; `com.unity.ai.navigation`'s cache was missing most `Runtime/*.cs`. Both embedded into `Packages/`.
- **8/4 — Android build target misconfigured.** Architecture was `x86_64` (Quest needs ARM64) and the build scene list pointed at the empty `SampleScene`. Fixed both.
- **8/4 — `NullReferenceException` in Meta's `FirstPersonLocomotor` every frame.** A leftover `SlideLocomotionBroadcaster` Building Block was sending events to an unused, uninitialized locomotor (real locomotion is `CompleteVRLocomotion`). Disabled the broadcaster.
- **8/4 — Weight plate capped at 50%.** `PlateFillPercent.cs` multiplied by `50f` instead of `100f`.
- **8/5 — Only the slider could be grabbed, not the cubes.** The cubes' `Rigidbody.excludeLayers` contained `Player`, the layer the whole hand rig lives on. Meta's grab system finds candidates through real trigger events (`OnTriggerEnter`), so the cubes never overlapped the hands — even though the Layer Collision Matrix looked fine. Cleared `excludeLayers`; also attached `playerPushScript` to the Player (scoped to the `Cubes` layer).
- **8/5 — Crouching in real life barely changed in-game height.** Only horizontal steps were amplified. Added `heightGain`, which amplifies vertical head movement and stacks on top of stick-crouch.
- **8/6 — Follow-up pass:**
  - *Standing up made the view drop.* The height-gain delta used `-=`, cancelling real motion. Correct sign is `+=`; still fades to 1× near the floor so reaching for ground objects stays precise.
  - *A-button "fly".* Jump fired any frame `CharacterController.isGrounded` was true, which re-fires off cube contacts, stacking impulses. Jump was removed (method renamed `HandleGravity`). *It later came back with a real sphere ground check on `groundLayer`.*
  - *Bare hand tracking triggered running from any gesture.* No grip button to gate on, and normal motion crossed `minSwing = 0.6`. Added `minSwingHands` (default `2.0` m/s) for hand-tracking mode.
  - *Player climbing on cubes.* `stepOffset = 0.2` → `0.05` in `playerPushScript`; cubes now act as walls.
  - *`DumbbellWeight.cs` deleted* (unused). If the Dumbbell shows `Missing (Mono Script)`, remove that row.
  - *Don't put a `MeshCollider` on the Dumbbell* — non-convex MeshColliders on Rigidbodies silently skip collision. Keep the `BoxCollider`.
- **8/11–8/12 — Project wouldn't compile: merge conflict markers.** `Logs/Editor-prev.log`: `error CS8300: Merge conflict marker encountered` 108× in `CompleteVRLocomotion.cs` and 45× in `FeelFollower.cs`, then `error CS0103: The name 'recenterButton' / 'recenterButton2' does not exist`. A pull left raw `<<<<<<<` / `=======` / `>>>>>>>` blocks, so nothing compiled. Resolved by hand. **Tip:** after any pull, search for `<<<<<<<` before opening Unity.
- **8/12 — Height buttons snapped back.** `HandleCrouch()` overwrote the camera's Y every frame, undoing B/X presses. Manual changes now go into a persistent `_manualHeightOffset` that `HandleCrouch()` applies each frame.
- **8/13–8/16 — `Package folder [...\Packages\com.meta.xr.sdk.core] missing package manifest`** (UPM warning, ~60×). See [Open Issues](#-open-issues).
- **8/14–8/16 — Package Manager couldn't reach Unity's servers on campus Wi-Fi.** `Logs/upm.log`: `Cannot connect to 'packages.unity.com' (ENOTFOUND/ENOENT)` and `ERR_TLS_CERT_ALTNAME_INVALID: Host: packages.unity.com. is not in the cert's altnames: DNS:wifisplash.sgrove.usmd.edu`. The Wi-Fi's captive portal was intercepting HTTPS because the laptop hadn't logged in. Fix: finish the Wi-Fi login in a browser, restart Unity. Cached packages keep working offline.

### September

- **9/12 — Merging diverged `main-temp` branches (30 conflicted files).** A never-pushed local commit (`e103b9e`, 8/11) was merged with the team's newer remote work (8/11–8/22):
  - **21 materials** — local had `_Color` reset to white, remote had the real tints → kept **remote**.
  - **`CompleteVRLocomotion.cs` / `GrabDetector.cs`** — remote had the 8/12 height fix and the two-hand `SetGrabState` manager → kept **remote**.
  - **`UMD CSPE.unity`** (52 blocks) — remote had 301 objects vs. 240 and matched the remote scripts; mixing halves of a scene breaks `fileID` references → whole file from **remote**. Not carried over: `Dumbbell` prefab, `Garage` prefab, `Directional Light` (recover with `git show 54b7c60:"Assets/Scenes/UMD CSPE.unity" > old.unity`).
  - **`OpenXR Package Settings.asset`** → remote (enables Subsampled Layout + Touch proximity). **`_Recovery` scenes** → remote. **ProBuilder `Settings.json`** → local (superset).
  - Verified: no markers left; all scripts compile with 0 errors (Unity 6000.5.4f1's Roslyn, offline).
- **9/12 — Old GitHub Desktop stash re-applied on top of the merge (3 new conflicts).** The stash `!!GitHub_Desktop<main-temp>` had been saved *before* the merge and was popped onto the merged branch. Conflicts in `README.md`, `Esp32Bridge.cs` and `UMD CSPE.unity`, plus 12 files changed automatically. Resolved:
  - **`Esp32Bridge.cs`** → stash logic: port `COM9`, weights rounded to whole numbers before sending (single-hand payloads used to send floats like `Lift,37.5,left`), weights reset to 0 on release and on Play.
  - **`UMD CSPE.unity`** (20 blocks) → stash tuning for 5 grabbable objects (linear/angular damping so held items move slower, `multiper` 0.6 / 1.5 / 0.29, a `GrabDetector` `_rigidbody` link, an added prefab child). **Except** 10 `MeshCollider`s the stash had switched to non-convex: all 10 sit on non-kinematic Rigidbodies, which Unity doesn't support (objects fall through the floor, grabs stop landing) → kept **Convex = on**.
  - **`ProjectSettings.asset`** — the stash removed `XRGeneralSettingsPerBuildTarget` and `OpenXRPackageSettings` from **Preloaded Assets**, which can stop XR from initializing in a Quest build → restored.
  - **Kept from the stash:** texture assignments on Apartment Kit materials (e.g. `Marble_Dark`, `Plastic_01`), URP global settings list populated by Unity, extra to-do items in the README.
- **9/12 — Scan fixes:**
  - **ESP32 ignored every command from Unity.** `ESP32.ino` compared `hand == "Left"` / `"Right"` (case-sensitive), but `Esp32Bridge` sends lowercase `left` / `right` / `both`, and `both` wasn't handled at all. Also Unity sends `Release` with weight `0`, so the right arm never pulsed back down. Firmware now matches case-insensitively, handles `both`, and remembers the right arm's current level: `Lift` moves up/down to the new level, `Release` pulses back to 0. **Re-flash the ESP32.**
  - **`FeelFollower.cs` couldn't be attached.** File `FeelFollower.cs`, class `FeetFollower` — Unity needs them to match. Renamed file + `.meta` to `FeetFollower.cs` (GUID kept, so nothing breaks).

---

## 🧰 Early / Undated Fixes

- Controller not appearing, and controller inputs not detected by the Meta XR interaction kit.
- Assets not loading into Unity (purple or max brightness).
- Hand-and-controller model blend glitch — fixed by treating hand-tracked and controller states independently instead of blending them.
- Real-life movement only translating ~3 cm in VR — fixed by measuring the real head-position delta and scaling it.
- Objects looking too big when picked up — fixed with grab transformer scripts that constrain scale.
- Blocks made solid via gravity + kinematic-while-grabbed, instead of making the *person* solid.
- Grey screen — fixed by disabling tunneling/passthrough scripts.
- Box colliders and camera clipping into the ground — fixed with real box colliders and a proper player controller instead of moving the bare camera.
- OpenXR/Meta Quest not running the scene on Play — deleted corrupted `com.unity.xr.openxr` cache files, then set OpenXR for Android.
- Cubes too bouncy / grabbing unrealistic — increased angular and linear damping.
- Block flying — the object's colliders become triggers while grabbed.
- Slow gravity / double jump — the capsule landed on its bottom edge after a failed jump; fixed with separate layers for ground detection.

---

<div align="center">

[⬅ Back to README](../README.md)

</div>
