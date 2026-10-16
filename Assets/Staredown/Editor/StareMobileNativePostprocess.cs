#if UNITY_EDITOR && UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace Staredown.Editor
{
    public static class StareMobileNativePostprocess
    {
        [PostProcessBuild(90)]
        public static void LinkNativeCamera(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS || !StareMobileBuild.BuildingMobile) return;
            string projectPath = PBXProject.GetPBXProjectPath(path);
            var project = new PBXProject(); project.ReadFromFile(projectPath);
            StareMobileIOSLifecycle.ValidateExport(path, project);
            string framework = project.GetUnityFrameworkTargetGuid();
            foreach (string name in new[] { "ARKit.framework", "CoreImage.framework", "UIKit.framework", "QuartzCore.framework", "AVFoundation.framework" })
                project.AddFrameworkToProject(framework, name, false);
            foreach (string native in new[] { "StaredownMobileCamera.mm", "StaredownAudioRoute.mm" })
            {
                string file = project.FindFileGuidByProjectPath("Libraries/Plugins/iOS/" + native);
                if (string.IsNullOrEmpty(file)) throw new System.Exception("Native iPhone bridge is missing from the Xcode project: " + native);
                var flags = project.GetCompileFlagsForFile(framework, file);
                if (!flags.Contains("-fobjc-arc")) flags.Add("-fobjc-arc");
                project.SetCompileFlagsForFile(framework, file, flags);
            }
            File.WriteAllText(projectPath, project.WriteToString());
            // Allow switching modes at runtime; the default PlayerSetting and
            // phone Awake both select portrait. Never offer upside-down UI.
            string plistPath = Path.Combine(path, "Info.plist");
            var plist = new PlistDocument(); plist.ReadFromFile(plistPath);
            foreach (string key in new[] { "UISupportedInterfaceOrientations", "UISupportedInterfaceOrientations~ipad" })
            {
                plist.root.values.Remove(key);
                var orientations = plist.root.CreateArray(key);
                orientations.AddString("UIInterfaceOrientationPortrait");
                orientations.AddString("UIInterfaceOrientationLandscapeLeft");
                orientations.AddString("UIInterfaceOrientationLandscapeRight");
            }
            plist.root.SetString("UIInterfaceOrientation", "UIInterfaceOrientationPortrait");
            File.WriteAllText(plistPath, plist.WriteToString());
            if (PhoneSdkPrivacyScope.Current == null) throw new System.Exception("SDK privacy declarations were not merged");
            PhoneSdkPrivacyScope.Current.WriteResourceBundle(path);
        }
    }
}
#endif
