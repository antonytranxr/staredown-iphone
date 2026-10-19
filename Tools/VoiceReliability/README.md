# Voice regression fixtures

Run `zsh Tools/VoiceReliability/run.sh` after package import (Mono/mcs required). It compiles the actual StareVoice class with mocked Unity/Vivox surfaces for iOS and Android permission paths. These are lifecycle tests, not native device/audio tests.

`VoicePanelChecks.snippet.cs` can be passed as the code argument of the Unity MCP execute_code tool in the running mobile scene. It opens Settings, yields a rendered UI frame, checks the real GraphicRaycaster hit for Voice check, opens the panel and verifies the repair action and close behavior. Its output path points to the original task's evidence directory; change that path for another checkout.

The checked-in editor checks cover capture-time blink events, retry receipts, authority validation, a rendered mirror control and an actual TCP phone client/host-protocol round. Use the phone scene with desktop XR initialization disabled; restore it after tests.
