using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif

// iOS 27 rejects the legacy UIApplication-only lifecycle at launch when linked
// with its SDK. Require Unity's maintained implementation, not a plist-only shim.
public static class StareMobileIOSLifecycle
{
 public const string RecommendedEditor="6000.3.26f1";
 public static void RequireCompatibleEditor()
 {
  RequireSceneTemplates(BuildPipeline.GetPlaybackEngineDirectory(BuildTarget.iOS,BuildOptions.None));
 }
 public static void RequireSceneTemplates(string playbackDirectory)
 {
  string ui=Path.Combine(playbackDirectory??"","Trampoline/Classes/UI");
  foreach(string file in new[]{"UnityScene.h","UnityScene.mm"})
   if(!File.Exists(Path.Combine(ui,file)))
    throw new BuildFailedException("iPhone export blocked: this Unity iOS module lacks UIScene lifecycle support and can crash at launch on iOS 27. Open the separate iPhone project with Unity "+RecommendedEditor+" LTS and matching iOS Build Support. Do not just add UIApplicationSceneManifest to an older export.");
 }
#if UNITY_IOS
 public static void ValidateExport(string path,PBXProject project)
 {
  var plist=new PlistDocument();plist.ReadFromFile(Path.Combine(path,"Info.plist"));
  PlistElement manifest,configs,application;
  if(!plist.root.values.TryGetValue("UIApplicationSceneManifest",out manifest)||!(manifest is PlistElementDict)||
     !manifest.AsDict().values.TryGetValue("UISceneConfigurations",out configs)||!(configs is PlistElementDict)||
     !configs.AsDict().values.TryGetValue("UIWindowSceneSessionRoleApplication",out application)||!(application is PlistElementArray)||application.AsArray().values.Count==0)
   throw new BuildFailedException("iPhone export has no application UIScene configuration. Re-export using Unity "+RecommendedEditor+" LTS with its default scene lifecycle enabled.");
  bool unityScene=false;
  foreach(var config in application.AsArray().values)
  {
   PlistElement delegateClass;
   if(config is PlistElementDict&&config.AsDict().values.TryGetValue("UISceneDelegateClassName",out delegateClass)&&delegateClass.AsString()=="UnityScene")unityScene=true;
  }
  if(!unityScene)throw new BuildFailedException("iPhone export must use Unity's UnityScene delegate.");
  foreach(string file in new[]{"UnityScene.h","UnityScene.mm"})
  {
   string relative="Classes/UI/"+file;
   if(!File.Exists(Path.Combine(path,relative))||string.IsNullOrEmpty(project.FindFileGuidByProjectPath(relative)))
    throw new BuildFailedException("iPhone export is missing the registered native lifecycle file: "+relative);
  }
 }
#endif
}
