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
