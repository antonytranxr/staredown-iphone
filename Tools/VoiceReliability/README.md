# Voice regression fixtures

Run `zsh Tools/VoiceReliability/run.sh` after package import (Mono/mcs required). It compiles the actual StareVoice class with mocked Unity/Vivox surfaces for iOS and Android permission paths. These are lifecycle tests, not native device/audio tests.

The 54 checks per platform cover denied/granted microphone permission, persistent manual mute, focus/pause, capture gain, unexpected SDK input mute, actual transmitting channel, independent receive recovery, missing/hot-plug input devices, room changes, and device/output/reconnect events arriving during an unfinished SDK operation. A quiet microphone must remain connected: absence of speech alone does not prove a broken microphone and never forces a rejoin. Participant energy updates are requested at 10 Hz; the raw signal meter is independent of the speech threshold used to animate mouths.

On a Quest that cannot be heard, inspect `MicrophoneSummary` and `DiagnosticsSummary`, or development logs beginning `[STARE VOICE MIC]`. They report permission, app focus, input device and SDK gain, transmitting state, raw local energy, and speech detection. If a speaking player has no captured energy while permission, focus and transmission are healthy, check the headset's system microphone setting and test another app. Nonzero local energy plus missing energy in another participant's receive diagnostics needs an actual two-device Vivox test; mocked checks cannot certify sound reaching a listener.

`VoicePanelChecks.snippet.cs` can be passed as the code argument of the Unity MCP execute_code tool in the running mobile scene. It opens Settings, yields a rendered UI frame, checks the real GraphicRaycaster hit for Voice check, opens the panel and verifies the repair action and close behavior. Its output path points to the original task's evidence directory; change that path for another checkout.

The checked-in editor checks cover capture-time blink events, retry receipts, authority validation, a rendered mirror control and an actual TCP phone client/host-protocol round. Use the phone scene with desktop XR initialization disabled; restore it after tests.
