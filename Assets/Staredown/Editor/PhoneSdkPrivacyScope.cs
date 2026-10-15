#if UNITY_EDITOR && UNITY_IOS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.iOS.Xcode;

// Scope SDK privacy files during iPhone export without modifying package sources.
public sealed class PhoneSdkPrivacyScope : IDisposable
{
    static readonly string[] Sources = {
        "Packages/com.unity.services.wire/Runtime/PrivacyInfo.xcprivacy",
        "Packages/com.unity.services.multiplayer/Runtime/PrivacyInfo.xcprivacy",
        "Packages/com.unity.services.authentication/Runtime/PrivacyInfo.xcprivacy",
        "Packages/com.unity.services.vivox/Plugins/iOS/PrivacyInfo.xcprivacy",
        "Packages/com.unity.services.qos/Runtime/PrivacyInfo.xcprivacy",
        "Packages/com.unity.services.core/Runtime/PrivacyInfo.xcprivacy"
    };
    readonly List<string> excluded = new List<string>();
    public static PhoneSdkPrivacyScope Current { get; private set; }
    public string MergedXml { get; private set; }
    static readonly HashSet<string> RootKeys = new HashSet<string> {
        "NSPrivacyTracking", "NSPrivacyTrackingDomains", "NSPrivacyAccessedAPITypes", "NSPrivacyCollectedDataTypes"
    };
    public static PhoneSdkPrivacyScope Begin()
    {
        if (Current != null) throw new InvalidOperationException("Privacy build scope already active");
        var scope = new PhoneSdkPrivacyScope();
        try {
            var merged = new PlistDocument();
            merged.root.SetBoolean("NSPrivacyTracking", false);
            merged.root.CreateArray("NSPrivacyTrackingDomains");
            merged.root.CreateArray("NSPrivacyAccessedAPITypes");
            merged.root.CreateArray("NSPrivacyCollectedDataTypes");
            foreach (string path in Sources) {
                var importer = FindPlugin(path);
                if (importer == null)
                    throw new InvalidOperationException("Cannot exclude privacy manifest: " + path + " / actual importer=" +
                        (AssetImporter.GetAtPath(path)?.GetType().FullName ?? "missing") + ". Restart after installing iOS module, then inspect PluginImporter.GetAllImporters().");
                var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(path);
                if (package == null) throw new InvalidOperationException("Missing package for " + path);
                string disk = Path.Combine(package.resolvedPath, path.Substring(package.assetPath.Length + 1));
                var source = new PlistDocument(); source.ReadFromFile(disk);
                MergeRoot(merged.root, source.root);
                importer.SetIncludeInBuildDelegate(_ => false);
                scope.excluded.Add(path);
            }
            scope.MergedXml = merged.WriteToString();
            Current = scope;
            return scope;
        } catch { scope.Dispose(); throw; }
    }
    public void Dispose()
    {
        // Package SDK manifests currently have no custom include delegates.
        // Reacquire importers after asset refreshes; do not save package metadata.
        foreach (string path in excluded) {
            var importer = FindPlugin(path);
            if (importer != null) importer.SetIncludeInBuildDelegate(null);
        }
        excluded.Clear();
        if (Current == this) Current = null;
    }
    static PluginImporter FindPlugin(string path)
    {
        return AssetImporter.GetAtPath(path) as PluginImporter ??
            PluginImporter.GetAllImporters().FirstOrDefault(importer => importer.assetPath == path);
    }
    // Invoke from the iOS postprocessor while Begin's scope is still active.
    // Adds a separate SDK declaration bundle, preserving Unity engine's file.
    public void WriteResourceBundle(string exportedProject)
    {
        const string relative = "StaredownUGSPrivacy.bundle";
        string bundle = Path.Combine(exportedProject, relative);
        Directory.CreateDirectory(bundle);
        File.WriteAllText(Path.Combine(bundle, "PrivacyInfo.xcprivacy"), MergedXml);
        var info = new PlistDocument(); info.root.SetString("CFBundleIdentifier", "com.antony.staredown.mobile.sdkprivacy"); info.root.SetString("CFBundlePackageType", "BNDL"); info.root.SetString("CFBundleVersion", "1"); info.WriteToFile(Path.Combine(bundle, "Info.plist"));
        string projectPath = PBXProject.GetPBXProjectPath(exportedProject);
        var project = new PBXProject(); project.ReadFromFile(projectPath);
        string guid = project.FindFileGuidByProjectPath(relative);
        if (string.IsNullOrEmpty(guid)) guid = project.AddFolderReference(relative, relative, PBXSourceTree.Source);
        project.AddFileToBuild(project.GetUnityMainTargetGuid(), guid);
        File.WriteAllText(projectPath, project.WriteToString());
    }
    static void MergeRoot(PlistElementDict destination, PlistElementDict source)
    {
        foreach (var key in source.values.Keys)
            if (!RootKeys.Contains(key)) throw new InvalidOperationException("Unhandled privacy manifest field " + key);
        PlistElement tracking;
        if (source.values.TryGetValue("NSPrivacyTracking", out tracking))
            destination.SetBoolean("NSPrivacyTracking", destination["NSPrivacyTracking"].AsBoolean() || tracking.AsBoolean());
        MergeStrings(destination["NSPrivacyTrackingDomains"].AsArray(), ArrayOrEmpty(source, "NSPrivacyTrackingDomains"));
        MergeCategories(destination["NSPrivacyAccessedAPITypes"].AsArray(), ArrayOrEmpty(source, "NSPrivacyAccessedAPITypes"),
            "NSPrivacyAccessedAPIType", "NSPrivacyAccessedAPITypeReasons", new string[0]);
        MergeCategories(destination["NSPrivacyCollectedDataTypes"].AsArray(), ArrayOrEmpty(source, "NSPrivacyCollectedDataTypes"),
            "NSPrivacyCollectedDataType", "NSPrivacyCollectedDataTypePurposes", new[] { "NSPrivacyCollectedDataTypeLinked", "NSPrivacyCollectedDataTypeTracking" });
    }
    static PlistElementArray ArrayOrEmpty(PlistElementDict root, string key)
    { PlistElement value; return root.values.TryGetValue(key, out value) ? value.AsArray() : new PlistElementArray(); }
    static void MergeStrings(PlistElementArray destination, PlistElementArray source)
    {
        var seen = new HashSet<string>(destination.values.Select(item => item.AsString()));
        foreach (var item in source.values) if (seen.Add(item.AsString())) destination.AddString(item.AsString());
    }
    static void MergeCategories(PlistElementArray destination, PlistElementArray source, string identity, string listKey, string[] flags)
    {
        var allowed = new HashSet<string>(flags.Concat(new[] { identity, listKey }));
        foreach (var item in source.values) {
            var incoming = item.AsDict();
            foreach (string key in incoming.values.Keys)
                if (!allowed.Contains(key)) throw new InvalidOperationException("Unhandled privacy category field " + key);
            string category = incoming[identity].AsString();
            var existing = destination.values.Select(value => value.AsDict()).FirstOrDefault(dict => dict[identity].AsString() == category);
            if (existing == null) { existing = destination.AddDict(); existing.SetString(identity, category); existing.CreateArray(listKey); }
            MergeStrings(existing[listKey].AsArray(), ArrayOrEmpty(incoming, listKey));
            foreach (string flag in flags) {
                PlistElement a, b;
                bool value = existing.values.TryGetValue(flag, out a) && a.AsBoolean();
                value |= incoming.values.TryGetValue(flag, out b) && b.AsBoolean();
                existing.SetBoolean(flag, value);
            }
        }
    }
}
#endif
