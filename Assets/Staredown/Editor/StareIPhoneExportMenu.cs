#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class StareIPhoneExportMenu
{
    [MenuItem("Staredown/Export iPhone App")]
    public static void Export()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)
        {
            EditorUtility.DisplayDialog("Export iPhone App", "Exit Play mode and finish the current build before exporting.", "OK");
            return;
        }
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS)
        {
            EditorUtility.DisplayDialog("Export iPhone App", "Install iOS Build Support and switch the active Build Profile to iOS first.", "OK");
            return;
        }
        string path = EditorUtility.SaveFolderPanel("Choose iPhone Xcode export folder", "", "Staredown-iOS");
        if (string.IsNullOrEmpty(path)) return;
        Debug.Log("[STARE IPHONE EXPORT] " + StareMobileBuild.IOS(path));
    }
}
#endif
