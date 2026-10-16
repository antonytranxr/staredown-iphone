#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class StareIPhoneExportMenu
{
    // Used by local/CI builds; a failed Unity build must fail the command.
    public static void ExportBatch()
    {
        string[] args=System.Environment.GetCommandLineArgs();
        int index=System.Array.IndexOf(args,"-stareExportPath");
        if(index<0||index+1>=args.Length)throw new System.ArgumentException("Pass -stareExportPath followed by a fresh Xcode export folder.");
        string path=System.IO.Path.GetFullPath(args[index+1]);
        string result=StareMobileBuild.IOS(path);
        System.IO.Directory.CreateDirectory(path);
        System.IO.File.WriteAllText(System.IO.Path.Combine(path,"staredown-export-result.txt"),result);
        if(!result.StartsWith("Succeeded"))throw new UnityEditor.Build.BuildFailedException(result);
        Debug.Log("[STARE IPHONE EXPORT] "+result);
    }

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
