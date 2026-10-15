#if UNITY_EDITOR && UNITY_IOS
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;

// Unused Meta Building Blocks retain native Avatar SDK calls on iOS, where
// this SDK distribution has no native library. The phone uses shared rules
// and preset characters; Quest builds keep their complete Meta integrations.
public sealed class StarePhoneAssemblies : IFilterBuildAssemblies
{
    public int callbackOrder => 1000002;
    public string[] OnFilterAssemblies(BuildOptions options, string[] assemblies)
    {
        if (!StareMobileBuild.BuildingMobile) return assemblies;
        return assemblies.Where(path => Path.GetFileName(path) != "Meta.XR.MultiplayerBlocks.Shared.dll" && Path.GetFileName(path) != "Oculus.AvatarSDK2.dll").ToArray();
    }
}
#endif
