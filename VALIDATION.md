# Camera reliability and compact gameplay UI — 2026-10-19

Version **1.1.4 (7)** preserves the player's camera-enabled choice across OS permission dialogs and foreground recovery. Explicit camera-off and first-launch opt-in remain respected. Joining or entering practice preserves capture; the home camera button opens its settings once enabled. The arena uses a slim room bar, a small expandable camera preview, temporary notices and Settings-only room/camera controls, leaving the center available for looking around.

The previous native bridge exposed only its latest eye sample. Slow Unity frames therefore discarded captured closures. JPEG encoding also ran on the serial eye-callback queue. The new bounded native sample queue preserves capture history, while a separate one-job encoder skips decorative video work when busy. Capture-time age rejects delayed data; overflow explicitly invalidates tracking. Supported 60 Hz capture is preferred, with 30 Hz fallback. At least two closed samples spanning 30 ms are required; the closure threshold still rejects squints and isolated peaks. A capture-time Go barrier preserves free countdown blinks.

Unity passed 58 detector cadence checks, including all 24 synthetic low-render-rate blink cases the latest-only path missed; 9 provider history/staleness/overflow checks; 4 camera recovery checks; 20 video stability/ownership checks; 98 orientation checks; 23 settings/entry checks; 19 touch/scene checks; 14 continuity checks; and 95 compact UI checks. Native policy checks passed 449 cases and arm64 iOS ARC syntax compilation passed. Live Relay tests with synthetic eyes verified authoritative blink elimination and face-absence loss while continuing video. Six portrait/landscape UI captures were reviewed.

The camera-head view keeps a clearly marked frozen image through transport jitter, rather than alternating between the video housing and avatar head. Camera-off/disconnect removes it immediately. Local preview reuse avoids a second JPEG decode. Phone mirror captures are reduced to 6 Hz, or 3 Hz under pressure; Quest cadence is unchanged. Settings exposes measured game, native eye/read and video rates plus blink/gap counters.

The game targets 30 FPS (24 under serious thermal pressure), independently of requested native eye capture and 6 FPS social-video thumbnails. Current JPEG/JSON host fanout is a prototype media path: with sixteen phone origins, a 30 FPS phone host can flush at most 60 camera messages per recipient each second while 96 are requested, before other bottlenecks. These are source-derived bounds, not device measurements. Fluid many-player video needs a separately designed hardware-codec/adaptive media path; raising the thumbnail cap alone is insufficient. See `MOBILE_STREAMING_BUDGET.json`.

Unity 6000.3.26f1 exported 1.1.4 (7) with zero errors and three warnings. All 33 export checks pass, including the new native batch-drain and capture-timestamp P/Invoke calls. Physical iPhone camera accuracy, actual achieved capture cadence, sustained heat/FPS and many-camera throughput remain unverified because no phone was online. No signed IPA or TestFlight upload was produced. See `MOBILE_RELIABILITY_VALIDATION.json`.

# Mobile controls and camera update — 2026-10-16

Version **1.1.3 (6)** starts in portrait and provides portrait/landscape selection in the always-visible Settings panel. Settings includes saved music mute/volume, voice/microphone, eye calibration and name controls. The home screen requires a valid name before practice, room creation or joining; it saves the visible name without another confirmation dialog.

The native ARKit provider now measures face orientation toward the camera instead of assuming a rear-camera basis. The live view uses the current interface display transform, refreshes orientation while running, removes mirroring from the canonical stream and mirrors only the local preview once. Calibration shows progress and specific guidance; closed eyes, turned faces and stale samples remain invalid. Start checks current camera readiness directly. Mirror capture targets 12 Hz with sustained frame pressure and phone thermal fallbacks; main phone rendering remains 30 FPS with 2× MSAA and the existing resolution cap.

The native policy harness passed 255 checks; iOS 15 arm64 ARC syntax compilation passed. Unity passed 98 pose/calibration/preview, 21 profile/settings, 19 scene/touch, 14 continuity and 76 UI raycast/layout checks. Portrait and landscape home/settings/lobby were captured and inspected. A real Relay room with synthetic camera input reached Duel, eliminated on a blink, and separately enforced face-absence loss while continuing video. Mirror cadence/budget simulation passed 766 checks.

Unity 6000.3.26f1 exported 1.1.3 (6) successfully with zero errors and three warnings. Twenty-nine export checks verify portrait default, both landscapes, no upside-down UI, native UnityScene lifecycle, ARKit/AVFoundation, native ARC, exact new camera source and generated orientation linkage. See `MOBILE_CONTROLS_VALIDATION.json`. Run `Tools/verify_mobile_export.py EXPORT --source-project PROJECT` to validate another export.

No new signed IPA or TestFlight upload was produced. Physical iPhone camera presentation, live calibration, microphone/speaker behavior and sustained heat/FPS still require device validation. Android phone packaging remains deferred. The shared Quest mirror source changed but no new Quest APK was built in this update.

# iOS 27 launch compatibility — 2026-10-16

The 1.1.1 (4) tester report traps in UIKit's no-scene-lifecycle check about 0.24 seconds after launch on iOS 27.0.1. The iPhone project now uses Unity 6000.3.26f1 LTS, which includes Unity's native UnityScene lifecycle. The next phone version is **1.1.2 (5)** with a minimum deployment target of **iOS 15**.

A fresh Unity export succeeded with zero errors and two warnings. Both older editor modules and the old Xcode export are rejected by the new guard; the new module and export pass. The generated application scene manifest, native UnityScene callbacks, UnityScene.mm Sources registration, native bridge ARC settings and iOS 15 deployment target were checked. Existing avatar/texture GUIDs are retained; the checked-in metadata and package lock changes are the editor's migration for this separate iPhone checkout. The working Quest checkout remains on its existing editor.

The iPhone was offline and no iOS 27 runtime was available locally. The rebuilt app still needs an iOS 27 cold-launch and background/resume/camera/voice test. No new native archive, IPA or TestFlight upload was produced. See `IOS27_LAUNCH.md` for replacement-build instructions and `IOS27_VALIDATION.json` for evidence.

# iPhone speaker and anti-aliasing update — 2026-10-15

Phone source 1.1.1 build 3 adds a native iOS speaker fallback. If the active PlayAndRecord session routes exclusively to the built-in receiver, with no external microphone, the helper selects the built-in speaker. It monitors audio route changes and recovery without changing Vivox's category, mode or session activation. Headphones, Bluetooth, AirPlay and USB routes remain untouched. Diagnostic logs record port types and category/mode changes without device names or identifiers.

The phone's main Built-in Forward camera now uses 2× MSAA, with HDR disabled. The existing 30 FPS / 1280-pixel-long-edge limits, thermal fallback and secondary-camera budgets remain. Quest rendering is unchanged.

Native arm64/iOS compilation, exported bridge symbols, route-policy checks and 64 mocked voice lifecycle checks passed. The mobile scene passed 19 basic and 14 camera-continuity checks. Twenty-five AA checks passed on Metal: explicit 1×/2× frozen arena captures differ at 48,485 pixels; the opaque diagonal fixture has 0/279 partially covered pixels. This validates the rendering setup and sample resolve, not sustained phone GPU performance.

Unity iPhone export 1.1.1 build 3 succeeded with zero errors. AVFoundation is linked and both native camera/audio bridges use ARC. Generated IL2CPP calls include the new audio entry points. No new native IPA or TestFlight upload has been produced. The paired iPhone was offline; speaker output, headphone transitions, voice quality and sustained heat/FPS still require physical-device validation. Android phone packaging remains deferred.

# Phone and room update — 2026-10-15

Phone source version 1.1.0 includes saved player names, voice-session initialization and real microphone permission checks, live video independent of face visibility, and reduced phone rendering/camera overhead.

Active phone contestants forfeit after one second of missing face tracking. Face loss before Go still pauses the countdown. Ordinary blinks during the countdown remain free. Video can continue while the face is absent; camera sharing still requires explicit enablement.

The live Relay test used a phone session and a Quest-protocol peer. Names propagated, the missing-face player lost in 1.01 model seconds, and five fresh JPEGs arrived during the absence. A second live round verified ordinary camera blink elimination. Camera inputs in these tests were synthetic.

Unity compilation and focused Editor checks passed, including 20 pause and first-loss ordering checks. Native ARKit bridge syntax and 52 mocked iOS/Android voice lifecycle checks passed. The phone session joined a real Vivox positional channel; physical microphone audio was not measured. Physical iPhone heat, sustained performance and native camera accuracy require a connected-device play test.

Phone rendering is capped at 30 FPS and 1280 pixels on the long edge, with smaller/less frequent mirror and jumbotron captures. Serious iOS thermal pressure lowers rendering to 24 FPS while retaining eye capture cadence. Unity phone builds use release build settings.

This source update does not imply a new TestFlight upload. Apple's updated SDK/toolchain and signing requirements still apply.

Unity iPhone export 1.1.0 build 2 succeeded with zero errors. The exported Xcode project includes the updated native camera bridge, microphone/camera usage declarations, ARKit linking, and SDK privacy bundle. A new native IPA and TestFlight upload have not been produced. Android phone packaging is intentionally deferred.
