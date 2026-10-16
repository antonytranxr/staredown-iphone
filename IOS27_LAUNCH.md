# iOS 27 launch crash — replacement build

The tester's 1.1.1 (4) TestFlight report crashes on the main thread about 0.24 seconds after launch in `___UIApplicationEvaluateRuntimeIssueForNoSceneLifecycleAdoption_block_invoke`. It is an iOS scene-lifecycle rejection before the game starts, not evidence of a blink, camera or networking failure.

Apple requires UIScene adoption for apps linked with the latest SDK and running on iOS 27. Unity 6000.2.7f2 exports the legacy application lifecycle. Adding only a scene manifest does not migrate Unity's initialization, window setup, pause/resume or display handling.

This iPhone repository now targets **Unity 6000.3.26f1 LTS** with its maintained native UnityScene implementation. Phone source version is **1.1.2**, next build number **5**. The upgraded editor exports a minimum deployment target of **iOS 15**; supported devices must run iOS 15 or later. Keep the working Quest checkout on its current editor.

## Build and verify

1. Pull this repository. Install Unity **6000.3.26f1 LTS**, **iOS Build Support**, and the Xcode version used by the TestFlight build machine. Open this iPhone checkout using the pinned Unity version.
2. Switch to iOS and use **Staredown > Export iPhone App** into a fresh folder. Do not append to the old build 4 Xcode project. The export guard rejects missing native scene templates, an absent/empty scene manifest, or missing registered UnityScene files.
3. Verify the export's `Info.plist` contains `UIApplicationSceneManifest > UISceneConfigurations > UIWindowSceneSessionRoleApplication` with `UISceneDelegateClassName = UnityScene`. Verify `Classes/UI/UnityScene.mm` belongs to the UnityFramework Sources phase. The native lifecycle must be present, not just the plist entry.
4. Archive and sign the fresh export as **1.1.2 (5)**. Never reuse the affected build 4 archive. Upload the new archive through the existing App Store Connect workflow.
5. On an iOS 27 device, test a cold launch, background/resume, camera and microphone permission prompts, camera/eye capture and a voice-enabled room. Then test the same flow on an older supported iOS device. Camera and microphone must pause/recover correctly with scene focus.

Local export validation cannot prove the tester's iOS 27 runtime is fixed. Only retesting the rebuilt app on that OS closes the incident. A source push is not a TestFlight upload.

## References

- [Apple: Transitioning to the UIKit scene-based lifecycle](https://developer.apple.com/documentation/uikit/transitioning-to-the-uikit-scene-based-life-cycle)
- [Unity: Update your editor to receive UIScene lifecycle support](https://discussions.unity.com/t/info-apple-update-your-editor-to-receive-uiscene-lifecycle-support/1709065)
- [Pinned Unity editor release](https://unity.com/releases/editor/whats-new/6000.3.26f1)
