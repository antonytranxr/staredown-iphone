# Crossplay reliability update — 2026-10-19

Target releases: Quest 1.18.3 (37), iPhone 1.1.5 (8).

## Fixes

- Phone blinks produce an event at capture time, before a later invalid sample can change the latest eye pose. A separate bounded outbox retries that event at 4 Hz until the host confirms it, for at most three seconds. The authority checks the transport sender, phone profile, capture eligibility, match, round, duel attempt, age and sequence. Previous-attempt events cannot eliminate players after a rematch; acknowledgements are idempotent. Countdown blink rules and the one-second missing-face forfeit remain.
- The phone view registers its local viewer, keeping its own body active for the mirror. Its opaque video-head housing uses layer 28, excluded from the main phone camera and included by the mirror. First-person head shader parameters update immediately on attachment. Local and remote social video retains the existing six-FPS budget; this update does not increase camera capture capability or video frame rate.
- This diagnostic build uses room-wide Vivox voice, removing spatial attenuation. It restores neutral voice output/channel gain on join, foreground recovery and output-device changes; a five-second health check catches muted/silent SDK output. Microphone permission, user mute and OS/device volume remain respected. Settings provide remote speaking activity, output/mic diagnostics and a reconnect control. Speech activity is SDK evidence, not proof that a human heard it.

## Compatibility

Room discovery is now `staredown-crossplatform-v8`. All participants must update both clients before joining a room. This prevents older positional-voice clients silently joining the game while listening to another voice channel. Numeric four-digit room codes remain unchanged. The optional blink capability handshake still supports the previous sequence input for protocol fixtures and direct transports.

## Verification

Automated results and physical-device limits are recorded in the release record and iPhone VALIDATION.md. In particular, synthetic camera samples do not establish real ARKit blink recall; four-person audibility across all twelve speaker/listener paths still requires the testers. A local Unity export is not a TestFlight upload.

## Quest build and install

The Android build succeeded in 4m11s. Unity counted twelve editor MCP WebSocket reconnection errors after the separate phone editor shut down that service; all twelve report error entries are exactly that editor bridge failure, not compiler, linker or player failures. The bridge is restored. The report retains those entries rather than relabeling it zero-error. APK package, OpenXR/Meta libraries and phone/Operator exclusions pass. Installed Quest 1.18.3 (37) matches the APK SHA-256. Startup detected real eye-tracking mode and reported 71.7/71.8 FPS before losing focus; no fatal or managed exception matched the scanned startup log. This is not sustained multiplayer or four-person audibility verification. See Validation/.

## Video delivery follow-up — 2026-10-20

Actual iPhone 12 Pro, installed 1.1.5 (8), sending to an Editor receiver through a cloud Relay room: 143 frames arrived over 40.21 seconds (3.56 FPS), while the sender's frame sequence advanced 236. The longest observed arrival gap was 1.07 seconds; receiver rendering stayed near 30 FPS. These measurements establish remote video loss/choppiness, not native eye-capture performance or blink recall. Shared video remains capped at six FPS independently of eye sampling.

UTP's generic fragmentation plus unreliable sequencing loses whole JPEGs when fragments reorder. New peers advertise video protocol 2 and exchange independent datagrams of at most 1016 bytes. Bounded reassembly accepts reordering and duplicates, expires partial frames after one second, and validates complete JPEGs before the existing authenticated camera-stream checks. Reliable gameplay still has priority. Old peers retain the legacy video pipeline, so room discovery remains v8; both ends need updated builds to gain the new delivery path.

Departed contestant roots are destroyed when their IDs leave the roster. Human identity diamonds share one mesh. Repeated joins no longer retain old avatars or allocate a new marker mesh each time.

Reproduce in an initialized mobile Play scene with `await StareMobileVideoSoak.Run(absoluteOutputPath, 12, seed)`. It exercises four synthetic JPEG streams and actual local UTP with clean, 60 ms / 15 ms jitter / 1% loss, and 150 ms / 40 ms jitter / 3% loss conditions, plus a planned camera pause and roster churn. It writes a bounded report and restores the session when finished. Editor timing is not physical phone FPS. `StareCameraPacketChecks.Run()` checks reorder, duplicates, incomplete expiry, malformed packets and allocation bounds.

Two candidate runs passed all four scenarios with no captured runtime errors. Longest unplanned video gaps were 0.51 s and 0.80 s, versus baseline gaps of 4.57–6.23 s under impaired conditions. The clean production pipeline, 16 camera transport checks, 17 packet checks, 33 camera protocol checks, 36 blink delivery/authority checks and legacy fallback in both directions passed. Actual legacy iPhone-to-new-receiver interoperability also worked during the live measurement. Updated physical builds and a post-install comparison remain separate from this synthetic verification.

### Physical follow-up

Installed iPhone 1.1.6 (9) after selecting the connected device for automatic development signing; the initial generic-device profile did not include this iPhone. Signed Debug native build, not TestFlight distribution. Installed Quest 1.18.4 (38); the installed APK hash matches the built APK. Its build succeeded with seven counted errors, all editor MCP WebSocket connection failures while the separate phone editor stopped the service. The local service was restored; no compiler/linker/player error appears among these seven report messages. Quest startup logs reached 71.8–71.9 FPS, but this is not sustained multiplayer validation.

Updated physical iPhone -> Editor host measurement over 40.15 seconds: 240 frames reached the transport across 240 source sequence advances (~5.98 FPS); host rate limiting accepted/displayed 227 (~5.65 FPS). Longest displayed-frame gap 0.47 seconds, p95 0.33 seconds; receiver rendering ~30 FPS. Protocol 2 negotiation was verified. Baseline used the iPhone as host and Editor as client, so this is a practical follow-up, not a fully controlled same-role benchmark. No many-camera or long-duration thermal result is claimed. Shared video is still six-FPS thumbnails. Seventeen additional live cloud Relay checks passed.
