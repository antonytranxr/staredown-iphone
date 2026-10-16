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
