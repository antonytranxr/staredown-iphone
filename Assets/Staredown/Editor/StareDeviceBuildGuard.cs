using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
// Meta's native-plugin selection reads the selected group rather than BuildPlayer's
// target. An earlier phone export can leave iPhone selected with Android active.
public sealed class StareQuestBuildContext : IPreprocessBuildWithReport
{
 public int callbackOrder=>-10000;
 public void OnPreprocessBuild(BuildReport report)
 {
  if(report.summary.platform==BuildTarget.Android&&!StareMobileBuild.BuildingMobile)
   EditorUserBuildSettings.selectedBuildTargetGroup=BuildTargetGroup.Android;
 }
}
// The SDK automatically bundles an experimental input-interposing automation
// layer in development APKs. Real-player tests use the unmodified Quest runtime.
public sealed class StareDeviceBuildGuard : IPreprocessBuildWithReport
{
 public int callbackOrder=>100;
 public void OnPreprocessBuild(BuildReport report)
 {
  if(report.summary.platform!=BuildTarget.Android)return;
  foreach(var importer in PluginImporter.GetAllImporters())
   if(importer.assetPath.Contains("XrApiLayer_METAX_operator_unity_android"))importer.SetIncludeInBuildDelegate(_=>false);
 }
}
