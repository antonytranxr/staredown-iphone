# Crossplay controls, lives and microphone recovery — 2026-10-20

Source targets iPhone 1.1.9 (12) and Quest 1.19.0 (40). Shared tournament checks pass for three lives with rotating opponents for actual rosters of 2–8; 9–16 retain knockout. The jumbotron displays standings and the saved champion. Private video stops sharing and purges queued frames without disabling eye tracking. Mobile has a persistent mic button, touch slingshot with independent movement, and shared bees, lightning, announcer, elimination effects and fireworks. Mirror targets rise to 24 Hz Quest / 15 Hz phone, with frame-budget and thermal fallbacks.

Validation passed: 122 lives/rotation checks, 16 privacy checks, 39 phone rules, 216 uninterrupted-countdown checks, 205 countdown checks, 36 captured-blink checks, 20 paused-phone checks, 17 video-packet checks, 33 camera-protocol checks, 35 mobile-control checks, 211 presentation checks, 157 phone UI checks, 20 eye HUD checks, 16 scoreboard presentation checks and 18 actual graphic-raycast slingshot/mic/shared-effect checks. Portrait and landscape screenshots were reviewed. These are Editor and synthetic checks, not physical touch feel or sustained device performance measurements.

An intermittent Quest send-only microphone failure is not reproduced on the affected device, which was unavailable. Code now repairs unexpected SDK input mute and low app input gain, preserves device/focus recovery events during asynchronous joins, and reports permission, device, gain, transmission and live mic activity. 54 mocked voice lifecycle checks passed for each of iOS and Android, including preserving explicit mute and background pause. Quiet audio never forces a reconnect. These tests do not prove native microphone capture or audibility.

Unity export succeeded with zero errors and three warnings in 61.7 seconds. All 33 export/native bridge checks passed. The signed native device build succeeded in 213.5 seconds; iPhone 1.1.9 (12) was installed, verified through the device app inventory and launched. Physical touch feel, native mic audibility and sustained mirror/thermal performance still require interactive testing. TestFlight was not updated.

# Simpler mobile start screen — 2026-10-20

Unity export succeeded with zero errors and three warnings, all 33 export checks passed, and native device signing succeeded. Installed 1.1.8 (11) was verified on the connected iPhone. The phone was locked, so automatic launch was denied; physical screen review remains for the user. TestFlight was not updated.

Phone 1.1.8 (11) gives player identity its own card, headed YOUR NAME with an Enter your name field and a short explanation. Room creation and joining live in a separate Play with friends card, with a specifically labeled four-digit code field. Practice is a secondary action. A dimmed arena backdrop and compact camera setup reduce visual clutter. Name saving, camera consent and direct entry behavior are preserved.

All 157 existing and focused UI checks passed, including actual touch raycasts, required name validation, disabled controls, settings, camera continuity and portrait/landscape safe areas. Sixteen 2x-MSAA screenshots include both empty and saved names. The identity and room entry groups do not overlap in either orientation. These are Editor layout/touch checks; physical keyboard and camera behavior were not re-certified by them.

# Mobile eye-status and uninterrupted countdown — 2026-10-20

Phone 1.1.7 (10) enlarges the selfie automatically for Countdown and Duel, with live per-eye OPEN/CLOSING/CLOSED/LOST labels, color feedback, concise rules and a shrinking missing-face grace bar. Eye indicators follow current local samples rather than the six-FPS shared thumbnail cadence. The preview returns to compact size after the contestant advances or is eliminated. Both portrait and landscape keep Settings, dance and the arena view accessible.

The shared host rule never pauses a countdown for mobile tracking loss. Blinks before Go remain free. After Go, one second without the face forfeits that contestant while other duels continue. Phone identity remains latched through camera-off and subsequent rounds. Quest hosts also need the matching 1.18.5 (39) update.

Passed 216 policy/identity/grace checks, 20 HUD checks, 38 mobile-room checks, 36 captured-blink checks, 205 countdown checks, 20 pause/order checks and 151 UI touch/layout/text-fit checks. Fourteen 2x-MSAA portrait/landscape captures were reviewed. Inputs were synthetic; physical eye classification and end-to-end play still need human testing.

Unity 6000.3.26f1 export: zero errors, three warnings. All 33 export checks pass. Native device-signed build succeeded; 1.1.7 (10) is installed and launched on the connected iPhone. TestFlight was not updated. Reports are in Validation/2026-10-20-mobile-eye-status/.

# Video delivery diagnostics — 2026-10-20

Candidate phone **1.1.6 (9)** uses capability-negotiated, independently framed video packets, avoiding UTP fragment ordering losses. Old peers retain legacy video and room v8 compatibility. Both endpoints need updated builds for the repaired delivery path. Departed contestant roots are released and human markers reuse one mesh.

Physical baseline: an iPhone 12 Pro on 1.1.5 (8) sent video into an actual Relay room. A receiving Editor client measured 143 video frames in 40.21 seconds (3.56 FPS), despite the source sequence advancing 236 frames. Longest receive gap was 1.07 seconds; the receiver's game stayed near 30 FPS. This demonstrates remote stream loss/choppiness. It does not measure native blink recall, sender GPU time, sustained heat or four-person audibility. Shared video remains capped at six FPS independently of eye sampling.

Two synthetic stress runs passed all four scenarios, with four concurrent JPEG origins, real local UTP, delayed/jittered/lossy packets, camera pause/recovery and twelve roster churn cycles each. Maximum unplanned video gap stayed below 0.80 seconds; baseline impaired-network gaps reached 6.23 seconds. Avatar count stayed bounded at sixteen and returned to zero after leaving. Seventeen packet checks, 33 protocol checks, 36 captured-blink checks, 16 camera transport checks and bidirectional legacy fallback passed. Editor timing is not physical phone FPS.

Unity 6000.3.26f1 exported 1.1.6 (9) with zero errors and three warnings. All 33 native scene/bridge/export checks pass; generated IL2CPP includes the new packet encoder and assembler. Native development signing succeeded and 1.1.6 (9) was installed on the connected iPhone. The post-install 40.15-second Relay run received 240 transport frames across 240 source sequence advances; host rate limiting displayed 227 (5.65 FPS), with a longest gap of 0.47 seconds. The baseline had different host/client roles; this is a practical follow-up rather than a controlled benchmark. The matching Quest 1.18.4 (38) is installed. No TestFlight upload was produced. Reports: `Validation/2026-10-20-mobile-video/`.

# Crossplay reliability — 2026-10-19

Phone 1.1.5 build 8 pairs with Quest 1.18.3. Mobile losing blinks now carry capture-time eligibility, match/round/duel scope and a retried host acknowledgement rather than depending on the latest face pose. The phone now renders its own body and live camera head in the mirror while excluding its housing from first person. Voice is room-wide for this test, with output gain/mute recovery, output-device observers, receive activity and a reconnect control.

All clients must update: room discovery is now `staredown-crossplatform-v8`, avoiding silent mixed positional/group voice channels. Four-digit room codes remain.

Verification passed: 36 captured-blink authority/delivery checks, 3 camera-event provenance checks, 58 cadence/noise checks, 9 provider history checks, 35 headset regressions, 33 camera protocol checks, 76 mocked voice lifecycle checks across iOS/Android permission paths, 95 portrait/landscape UI checks and 5 voice-popup touch checks. A rendered mirror versus hidden-local-avatar control differed at 27,224 pixels. An actual TCP phone client lost by blink despite its latest sample being invalid. Live Lobby/Relay v8 and Vivox group join, gain repair, rejoin and microphone-mute preservation passed; this one-participant SDK check ran with editor audio muted and does not establish audibility.

Unity 6000.3.26f1 exported 1.1.5 (8) successfully. All 33 scene lifecycle/native bridge/export checks passed. Generated IL2CPP includes the new blink delivery, host adjudication, local-head visibility and voice reconnect methods. Reports are in `Validation/`.

No signed IPA or TestFlight upload was produced. The local toolchain remains Xcode 16.2. Physical iPhone ARKit blink accuracy, sustained performance/heat and four-person audio across all twelve directed speaker/listener paths remain unverified. Social video remains capped at six FPS; this build fixes lost captured events, rather than claiming higher camera or video rates.

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
