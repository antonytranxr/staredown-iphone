# Staredown for iPhone

Cross-platform staring contests with players on iPhone and Meta Quest. The iPhone client provides touch movement, a front-camera preview, blink detection, and a live camera image on the player's in-arena avatar.

This is the editable Unity project, including the shared arena and VR code required by the phone client. This iPhone checkout is pinned to Unity 6000.3.26f1 LTS for native UIScene lifecycle support. The separate working Quest checkout remains on its existing editor. Generated Xcode output, signed apps, signing certificates, provisioning profiles, local credentials, and Unity caches are excluded. This repository is private; bundled third-party assets retain their original licenses.

## Build the iPhone app

1. Install **Unity 6000.3.26f1 LTS** with **iOS Build Support**, and Xcode on a compatible Mac.
2. Clone this repository and open its root in Unity Hub. Accept the editor upgrade for this iPhone checkout if prompted, then allow package resolution and asset import to finish. Meta packages resolve through the scoped registry in `Packages/manifest.json`.
3. In **File > Build Profiles**, switch the active platform to **iOS**. Exit Play mode.
4. Select **Staredown > Export iPhone App**, and choose an empty export folder outside `Assets`.
5. Open the exported `Unity-iPhone.xcodeproj` in Xcode. Select your Apple developer team and configure signing for the app target. The app bundle identifier is `com.antony.staredown.mobile`; change it in `StareMobileBuild.cs` if your team needs a different identifier.
6. Select a connected iPhone and run. Allow camera access to enable blink detection. For distribution, use Xcode Archive and your team's App Store Connect configuration with Apple's currently required Xcode and iOS SDK versions.

Use the supplied export command rather than Unity's generic Build button. It selects `StaredownMobile.unity`, adds `STARE_MOBILE`, excludes headset-only runtime resources, links the native camera frameworks, and merges SDK privacy declarations. It restores the shared project's original settings afterward. The export now rejects iOS modules without UnityScene templates and verifies the generated scene manifest and native scene delegate files. See `IOS27_LAUNCH.md` for the build 4 crash and replacement build checklist. Phone exports now use release build settings. See `VALIDATION.md` for speaker routing, anti-aliasing, camera, microphone, names, and rendering changes in version 1.1.1.

The Mac used for the original export had Xcode 16.2/iOS SDK 18.2. Native compilation and development signing succeeded there; that toolchain was not sufficient for the App Store Connect requirements encountered during the earlier TestFlight attempt. No TestFlight upload is included in this repository.

## Device and online setup

- Automatic blink detection uses `ARFaceTrackingConfiguration`; the device must support ARKit face tracking. Test on a physical compatible iPhone, facing the front camera, in adequate lighting. The iOS Simulator does not validate this camera path.
- Multiplayer uses Unity Authentication, Lobby, and Relay. The checked-in public Unity project identifiers refer to the original game's services. To operate an independent deployment, link the project to your Unity Cloud project and enable/configure those services. Never put service secrets in source control.
- Current source uses **four-digit room codes** and the `staredown-crossplatform-v7` directory. Quest and phone clients must use matching protocol versions. Older prebuilt phone apps are not equivalent to this source snapshot.
- Live camera sharing sends a compressed front-camera image to players in the room when enabled. Review the camera UI, privacy disclosure, and service settings before distributing.
- Voice chat uses the shared Vivox integration; availability depends on the deployment's Unity service configuration.

## Source map

| Path | Purpose |
| --- | --- |
| `Assets/Staredown/Scenes/StaredownMobile.unity` | Phone scene |
| `Assets/Staredown/Scripts/StareMobileSession.cs` | Phone match and room lifecycle |
| `Assets/Staredown/Scripts/StareMobileHud.cs` | Touch controls and camera preview |
| `Assets/Staredown/Scripts/StareMobileCamera.cs` | Camera provider integration |
| `Assets/Staredown/Scripts/StareMobileBlink.cs` | Blink timing and calibration |
| `Assets/Plugins/iOS/StaredownMobileCamera.mm` | Native ARKit camera/eye closure bridge |
| `Assets/Staredown/Scripts/StareRelayNetwork.cs` | Shared online room protocol |
| `Assets/Staredown/Editor/StareMobileBuild.cs` | Phone build pipeline |
| `Assets/Staredown/Editor/PhoneSdkPrivacyScope.cs` | SDK privacy manifest merge |
| `ThirdPartyNotices` | Supplementary asset and animation credits |

## Validation status

The earlier iPhone export and signed native development build succeeded. Automated shared-protocol and synthetic camera/blink tests passed. Physical iPhone camera accuracy, installation, and sustained performance still need device validation. The latest Unity iPhone export is version 1.1.2 build 5 from Unity 6000.3.26f1 LTS and passed its native scene lifecycle checks; no new native IPA or TestFlight upload has been produced. See `VALIDATION.md`.

`SOURCE_SNAPSHOT.json` identifies the local source checkpoint used for this upload. No open-source license is granted to the game or bundled third-party content by uploading it here.
