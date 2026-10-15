// In each phone build: using (var hidden = PhoneMetaResources.Hide()) { BuildPlayer(...); }
// Keep inside the existing settings-restoring try/finally. Hide before SaveAssets.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;

public sealed class PhoneMetaResources : IDisposable
{
    readonly List<KeyValuePair<string,string>> moved = new List<KeyValuePair<string,string>>();
    const string root = "Assets/Staredown/Resources/";
    const string hold = "Assets/Staredown/Editor/PhoneExcludedMeta";
    public static PhoneMetaResources Hide()
    {
        var scope = new PhoneMetaResources();
        if (!AssetDatabase.IsValidFolder(hold)) AssetDatabase.CreateFolder("Assets/Staredown/Editor", "PhoneExcludedMeta");
        try
        {
            foreach (string name in new[] { "StareMetaAvatarConfig.asset", "StareMetaAvatarRuntime.prefab", "StareMetaAvatarShader.asset", "StareMetaAvatarVertexShader.asset" })
            {
                string from = root + name, to = hold + "/" + name;
                if (!File.Exists(from)) throw new Exception("Expected Quest avatar resource missing: " + from);
                if (File.Exists(to)) throw new Exception("A previous phone build has not restored " + to);
                string error = AssetDatabase.MoveAsset(from, to);
                if (!string.IsNullOrEmpty(error)) throw new Exception("Could not exclude Quest-only resource: " + error);
                scope.moved.Add(new KeyValuePair<string,string>(from, to));
            }
            AssetDatabase.SaveAssets();
            return scope;
        }
        catch { scope.Dispose(); throw; }
    }
    public void Dispose()
    {
        var failures = new List<string>();
        for (int i = moved.Count - 1; i >= 0; i--)
        {
            var item = moved[i]; string error = AssetDatabase.MoveAsset(item.Value, item.Key);
            if (!string.IsNullOrEmpty(error)) failures.Add(item.Key + ": " + error);
        }
        moved.Clear(); AssetDatabase.SaveAssets();
        if (failures.Count > 0) throw new Exception("Restore Quest avatar resources before another build: " + string.Join("; ", failures));
    }
}
