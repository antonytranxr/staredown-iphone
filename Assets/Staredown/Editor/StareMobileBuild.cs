using System;
using System.IO;
using System.Linq;
using System.Xml;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
#if UNITY_ANDROID
using UnityEditor.Android;
#endif
using UnityEditor.SceneManagement;
using UnityEngine;
using Staredown;

public static class StareMobileBuild
{
 public const string Scene="Assets/Staredown/Scenes/StaredownMobile.unity";
 public static bool BuildingMobile {get;private set;}
 const string PhoneManifest="<?xml version=\"1.0\" encoding=\"utf-8\"?><manifest xmlns:android=\"http://schemas.android.com/apk/res/android\" android:installLocation=\"auto\"><uses-permission android:name=\"android.permission.INTERNET\"/><uses-permission android:name=\"android.permission.CAMERA\"/><uses-feature android:name=\"android.hardware.camera.front\" android:required=\"false\"/><application android:label=\"@string/app_name\" android:icon=\"@mipmap/app_icon\" android:allowBackup=\"false\"><activity android:name=\"com.unity3d.player.UnityPlayerGameActivity\" android:theme=\"@style/BaseUnityGameActivityTheme\" android:configChanges=\"locale|fontScale|keyboard|keyboardHidden|mcc|mnc|navigation|orientation|screenLayout|screenSize|smallestScreenSize|touchscreen|uiMode\" android:launchMode=\"singleTask\" android:exported=\"true\"><intent-filter><action android:name=\"android.intent.action.MAIN\"/><category android:name=\"android.intent.category.LAUNCHER\"/></intent-filter><meta-data android:name=\"unityplayer.UnityActivity\" android:value=\"true\"/></activity><meta-data android:name=\"unityplayer.SkipPermissionsDialog\" android:value=\"true\"/></application></manifest>";
 [MenuItem("Staredown/Create Mobile Scene")]
 public static void CreateScene()
 {
  if(EditorApplication.isPlaying)throw new Exception("Exit Play before creating the phone scene");
  var previous=SceneManagerSetup();
  try
  {
   EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
   var camera=new GameObject("Mobile arena camera").AddComponent<Camera>();camera.gameObject.AddComponent<AudioListener>();camera.tag="MainCamera";camera.transform.position=new Vector3(0,1.6f,-10);camera.clearFlags=CameraClearFlags.Skybox;camera.farClipPlane=160;camera.stereoTargetEye=StereoTargetEyeMask.None;
   var key=new GameObject("Mobile arena light").AddComponent<Light>();key.type=LightType.Directional;key.intensity=1.15f;key.shadows=LightShadows.None;key.transform.rotation=Quaternion.Euler(38,-28,0);
   new GameObject("STAREDOWN / Mobile").AddComponent<StareMobileSession>().ViewCamera=camera;
   EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),Scene);
  }
  finally{EditorSceneManager.RestoreSceneManagerSetup(previous);}
 }
 static UnityEditor.SceneManagement.SceneSetup[] SceneManagerSetup()=>EditorSceneManager.GetSceneManagerSetup();
 public static string Android(string apk)
 {
  if(EditorApplication.isPlayingOrWillChangePlaymode||BuildPipeline.isBuildingPlayer)throw new Exception("Finish Play/build before phone build");
  if(!File.Exists(Scene))CreateScene();
  var target=UnityEditor.Build.NamedBuildTarget.Android;
  string id=PlayerSettings.GetApplicationIdentifier(target),version=PlayerSettings.bundleVersion,product=PlayerSettings.productName;
  int code=PlayerSettings.Android.bundleVersionCode;
  var minimum=PlayerSettings.Android.minSdkVersion;var orientation=PlayerSettings.defaultInterfaceOrientation;
  var xr=UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);bool init=xr.InitManagerOnStart;var loaders=xr.Manager.activeLoaders.Select(loader=>new[]{AssetDatabase.GetAssetPath(loader),loader.name,loader.GetType().FullName}).ToArray();
  string manifest="Assets/Plugins/Android/AndroidManifest.xml";byte[] vrManifest=File.ReadAllBytes(manifest);
  var plugin=AssetImporter.GetAtPath("Assets/Plugins/Android/StaredownMobile.androidlib") as PluginImporter;
  if(plugin==null)throw new Exception("Phone camera plugin was not imported");bool compatible=plugin.GetCompatibleWithPlatform(BuildTarget.Android);var constraints=plugin.DefineConstraints;var graphics=PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);bool defaultGraphics=PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android);
  try
  {
   BuildingMobile=true;xr.InitManagerOnStart=false;xr.Manager.TrySetLoaders(new System.Collections.Generic.List<UnityEngine.XR.Management.XRLoader>());EditorUtility.SetDirty(xr.Manager);EditorUtility.SetDirty(xr);
   PlayerSettings.SetApplicationIdentifier(target,"com.antony.staredown.mobile");PlayerSettings.bundleVersion="1.0.0";PlayerSettings.productName="Staredown Mobile";PlayerSettings.Android.bundleVersionCode=1;PlayerSettings.Android.minSdkVersion=AndroidSdkVersions.AndroidApiLevel26;PlayerSettings.defaultInterfaceOrientation=UIOrientation.LandscapeLeft;
   PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android,false);PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,new[]{UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3});plugin.DefineConstraints=new string[0];plugin.SetCompatibleWithPlatform(BuildTarget.Android,true);plugin.SaveAndReimport();
   File.WriteAllText(manifest,PhoneManifest);AssetDatabase.ImportAsset(manifest,ImportAssetOptions.ForceUpdate);AssetDatabase.SaveAssets();
   var options=new BuildPlayerOptions{scenes=new[]{Scene},locationPathName=apk,target=BuildTarget.Android,options=BuildOptions.Development,extraScriptingDefines=new[]{"STARE_MOBILE"}};
   var report=BuildPipeline.BuildPlayer(options);
   return report.summary.result+" errors="+report.summary.totalErrors+" warnings="+report.summary.totalWarnings+" duration="+report.summary.totalTime;
  }
  finally
  {
   BuildingMobile=false;File.WriteAllBytes(manifest,vrManifest);AssetDatabase.ImportAsset(manifest,ImportAssetOptions.ForceUpdate);
   plugin.DefineConstraints=constraints;plugin.SetCompatibleWithPlatform(BuildTarget.Android,compatible);PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,graphics);PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android,defaultGraphics);plugin.SaveAndReimport();
   // Build/import steps can reload ScriptableObjects. Restore fresh loader
   // assets by their stable path/name, not the pre-build managed references.
   var restored=UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
   var fresh=new System.Collections.Generic.List<UnityEngine.XR.Management.XRLoader>();
   foreach(var key in loaders){var loader=AssetDatabase.LoadAllAssetsAtPath(key[0]).OfType<UnityEngine.XR.Management.XRLoader>().FirstOrDefault(item=>item.name==key[1]);if(loader==null)throw new Exception("Could not restore XR loader "+key[1]);fresh.Add(loader);}
   if(!restored.Manager.TrySetLoaders(fresh))throw new Exception("Could not restore phone build's original XR loaders");
   restored.InitManagerOnStart=init;EditorUtility.SetDirty(restored.Manager);EditorUtility.SetDirty(restored);
   PlayerSettings.SetApplicationIdentifier(target,id);PlayerSettings.bundleVersion=version;PlayerSettings.productName=product;PlayerSettings.Android.bundleVersionCode=code;PlayerSettings.Android.minSdkVersion=minimum;PlayerSettings.defaultInterfaceOrientation=orientation;AssetDatabase.SaveAssets();
  }
 }
 public static string IOS(string path)
 {
  if(EditorApplication.isPlayingOrWillChangePlaymode||BuildPipeline.isBuildingPlayer)throw new Exception("Finish Play/build before iPhone export");
  if(!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS,BuildTarget.iOS))throw new Exception("Install matching Unity iOS Build Support through Unity Hub.");
  if(EditorUserBuildSettings.activeBuildTarget!=BuildTarget.iOS)throw new Exception("Switch the Unity Editor active platform to iOS before exporting the phone app.");
  if(!File.Exists(Scene))CreateScene();
  var target=UnityEditor.Build.NamedBuildTarget.iOS;var id=PlayerSettings.GetApplicationIdentifier(target);var description=PlayerSettings.iOS.cameraUsageDescription;var microphone=PlayerSettings.iOS.microphoneUsageDescription;var stripping=PlayerSettings.GetManagedStrippingLevel(target);var minimum=PlayerSettings.iOS.targetOSVersionString;var version=PlayerSettings.bundleVersion;var product=PlayerSettings.productName;var orientation=PlayerSettings.defaultInterfaceOrientation;
  const string nativePath="Assets/Plugins/iOS/StaredownMobileCamera.mm";
  var plugin=AssetImporter.GetAtPath(nativePath) as PluginImporter;if(plugin==null)throw new Exception("iPhone camera bridge was not imported");
  var constraints=plugin.DefineConstraints;bool compatible=plugin.GetCompatibleWithPlatform(BuildTarget.iOS);
  var xr=UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.iOS);
  bool init=xr!=null&&xr.InitManagerOnStart;
  var loaders=xr!=null&&xr.Manager!=null?xr.Manager.activeLoaders.Select(loader=>new[]{AssetDatabase.GetAssetPath(loader),loader.name}).ToArray():new string[0][];
  try
  {
   BuildingMobile=true;PlayerSettings.SetManagedStrippingLevel(target,ManagedStrippingLevel.High);
   if(xr!=null){xr.InitManagerOnStart=false;EditorUtility.SetDirty(xr);if(xr.Manager!=null){xr.Manager.TrySetLoaders(new System.Collections.Generic.List<UnityEngine.XR.Management.XRLoader>());EditorUtility.SetDirty(xr.Manager);}}
   PlayerSettings.SetApplicationIdentifier(target,"com.antony.staredown.mobile");PlayerSettings.bundleVersion="1.0.0";PlayerSettings.productName="Staredown Mobile";PlayerSettings.iOS.targetOSVersionString="14.0";PlayerSettings.iOS.cameraUsageDescription="Staredown uses your front camera to detect blinks and show your live face to players in your current room when you enable the camera.";PlayerSettings.iOS.microphoneUsageDescription="Staredown uses the microphone for optional in-game voice chat.";PlayerSettings.defaultInterfaceOrientation=UIOrientation.LandscapeLeft;
   plugin.DefineConstraints=new string[0];plugin.SetCompatibleWithPlatform(BuildTarget.iOS,true);plugin.SaveAndReimport();
   #if UNITY_IOS
   using(var privacy=PhoneSdkPrivacyScope.Begin())
   #endif
   using(var hidden=PhoneMetaResources.Hide())
   {
    AssetDatabase.SaveAssets();
    var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Scene},locationPathName=path,target=BuildTarget.iOS,extraScriptingDefines=new[]{"STARE_MOBILE"},options=BuildOptions.Development});
    return report.summary.result+" errors="+report.summary.totalErrors+" warnings="+report.summary.totalWarnings+" duration="+report.summary.totalTime;
   }
  }
  finally
  {
   BuildingMobile=false;
   var native=AssetImporter.GetAtPath(nativePath) as PluginImporter;native.DefineConstraints=constraints;native.SetCompatibleWithPlatform(BuildTarget.iOS,compatible);native.SaveAndReimport();
   var restored=UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.iOS);
   if(restored!=null){restored.InitManagerOnStart=init;EditorUtility.SetDirty(restored);if(restored.Manager!=null){var fresh=loaders.Select(key=>AssetDatabase.LoadAllAssetsAtPath(key[0]).OfType<UnityEngine.XR.Management.XRLoader>().FirstOrDefault(loader=>loader.name==key[1])).ToList();if(fresh.Any(loader=>loader==null)||!restored.Manager.TrySetLoaders(fresh))throw new Exception("Could not restore iOS XR settings");EditorUtility.SetDirty(restored.Manager);}}
   PlayerSettings.SetApplicationIdentifier(target,id);PlayerSettings.bundleVersion=version;PlayerSettings.productName=product;PlayerSettings.iOS.cameraUsageDescription=description;PlayerSettings.iOS.microphoneUsageDescription=microphone;PlayerSettings.SetManagedStrippingLevel(target,stripping);PlayerSettings.iOS.targetOSVersionString=minimum;PlayerSettings.defaultInterfaceOrientation=orientation;AssetDatabase.SaveAssets();
  }
 }

}
// Meta's generator also runs for non-XR Android builds. Remove its hardware
// requirements AFTER generation so an ordinary phone can install this APK.
#if UNITY_ANDROID
public sealed class StareMobileManifestGuard : IPostGenerateGradleAndroidProject,IPreprocessBuildWithReport
{
 public int callbackOrder=>1000001;
 public void OnPreprocessBuild(BuildReport report)
 {
  var plugin=AssetImporter.GetAtPath("Assets/Plugins/Android/StaredownMobile.androidlib") as PluginImporter;
  if(plugin!=null)plugin.SetIncludeInBuildDelegate(_=>StareMobileBuild.BuildingMobile);
 }
 public void OnPostGenerateGradleAndroidProject(string path)
 {
  if(!StareMobileBuild.BuildingMobile)return;
  string root=Directory.GetParent(path).FullName;
  foreach(string file in Directory.GetFiles(root,"AndroidManifest.xml",SearchOption.AllDirectories))
  {
   var doc=new XmlDocument();doc.Load(file);var all=doc.SelectNodes("//*").Cast<XmlNode>().ToArray();
   foreach(var node in all)
   {
    string name=node.Attributes?["name","http://schemas.android.com/apk/res/android"]?.Value??"";
    if(node.NamespaceURI=="http://schemas.horizonos/sdk"||node.LocalName=="uses-feature"&&name=="android.hardware.vr.headtracking"||node.LocalName=="category"&&name=="com.oculus.intent.category.VR"||node.LocalName=="meta-data"&&(name.StartsWith("com.oculus.")||name.StartsWith("com.meta."))||node.LocalName=="uses-permission"&&name.StartsWith("com.oculus."))node.ParentNode?.RemoveChild(node);
   }
   doc.Save(file);
  }
 }
}

#endif
