# Phone and room update — 2026-10-15

Phone source version 1.1.0 includes saved player names, voice-session initialization and real microphone permission checks, live video independent of face visibility, and reduced phone rendering/camera overhead.

Active phone contestants forfeit after one second of missing face tracking. Face loss before Go still pauses the countdown. Ordinary blinks during the countdown remain free. Video can continue while the face is absent; camera sharing still requires explicit enablement.

The live Relay test used a phone session and a Quest-protocol peer. Names propagated, the missing-face player lost in 1.01 model seconds, and five fresh JPEGs arrived during the absence. A second live round verified ordinary camera blink elimination. Camera inputs in these tests were synthetic.

Unity compilation and focused Editor checks passed, including 20 pause and first-loss ordering checks. Native ARKit bridge syntax and 52 mocked iOS/Android voice lifecycle checks passed. The phone session joined a real Vivox positional channel; physical microphone audio was not measured. Physical iPhone heat, sustained performance and native camera accuracy require a connected-device play test.

Phone rendering is capped at 30 FPS and 1280 pixels on the long edge, with smaller/less frequent mirror and jumbotron captures. Serious iOS thermal pressure lowers rendering to 24 FPS while retaining eye capture cadence. Unity phone builds use release build settings.

This source update does not imply a new TestFlight upload. Apple's updated SDK/toolchain and signing requirements still apply.

Unity iPhone export 1.1.0 build 2 succeeded with zero errors. The exported Xcode project includes the updated native camera bridge, microphone/camera usage declarations, ARKit linking, and SDK privacy bundle. A new native IPA and TestFlight upload have not been produced. Android phone packaging is intentionally deferred.
