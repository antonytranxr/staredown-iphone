using System;
using System.IO;
using Oculus.Avatar2;
using Oculus.Platform;
using UnityEditor;
using UnityEngine;

namespace Staredown.Editor
{
    public sealed class StareMetaAvatarSetup : EditorWindow
    {
        const string Folder = "Assets/Staredown/Resources";
        const string ConfigPath = Folder + "/StareMetaAvatarConfig.asset";
        const string ManagerPath = Folder + "/StareMetaAvatarRuntime.prefab";
        const string Package = "Packages/com.meta.xr.sdk.avatars/";
        string appId = "", message = "";

        [MenuItem("Staredown/Configure Meta Avatar")]
        public static void ShowSetup()
        {
            var window = GetWindow<StareMetaAvatarSetup>("Meta Avatar Setup");
            window.minSize = new Vector2(480, 330);
            var config = AssetDatabase.LoadAssetAtPath<StareMetaAvatarConfig>(ConfigPath);
            window.appId = config != null ? config.MetaAppId : "";
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Your Meta Avatar on Quest", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Create a Meta Quest app, then enter its public App ID. No app secret or access token belongs in this project.", MessageType.Info);
            appId = EditorGUILayout.TextField("Quest App ID", appId);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("In the Meta developer dashboard:");
            EditorGUILayout.LabelField("• Grant User ID, User Profile and Avatars for Use Avatars.");
            EditorGUILayout.LabelField("• Add your headset account to the app's test release channel.");
            EditorGUILayout.LabelField("• Create a Meta Avatar for that headset account.");
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Personal avatars must be tested in the Quest build. Meta Avatars SDK 40.0.1 does not include a macOS native runtime; the four game characters work in Mac Play mode.", MessageType.None);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("Save Quest App ID"))
                {
                    if (!StareMetaAvatars.ValidId(appId.Trim())) message = "Enter a valid numeric Meta Quest App ID.";
                    else
                    {
                        PrepareAssets();
                        var config = AssetDatabase.LoadAssetAtPath<StareMetaAvatarConfig>(ConfigPath);
                        config.MetaAppId = appId.Trim();
                        PlatformSettings.MobileAppID = config.MetaAppId;
                        EditorUtility.SetDirty(config);
                        EditorUtility.SetDirty(PlatformSettings.Instance);
                        AssetDatabase.SaveAssets();
                        message = "Saved. Build to Quest, then choose Meta Avatar and connect.";
                    }
                }
                if (GUILayout.Button("Prepare / Verify Avatar Render Assets"))
                {
                    try { PrepareAssets(); message = "Avatar renderer prepared. Existing App ID preserved."; }
                    catch (Exception e) { message = "Could not prepare renderer: " + e.Message; }
                }
            }
            if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, MessageType.Info);
        }

        // Called explicitly after package installation/compilation. It does not start native Avatar services.
        public static void PrepareAssets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode before preparing render assets.");
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            var config = AssetDatabase.LoadAssetAtPath<StareMetaAvatarConfig>(ConfigPath);
            if (config == null)
            {
                config = CreateInstance<StareMetaAvatarConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }

            var full = CopyLightingConfig("MetaStyle2ShaderConfiguration", "StareMetaAvatarShader");
            var vertex = CopyLightingConfig("MetaStyle2ShaderConfigurationVertex", "StareMetaAvatarVertexShader");
            var cel = Load<OvrAvatarShaderConfiguration>("Scripts/Common/Shaders/Configurations/Recommended/MetaShaderCelConfiguration.asset");
            var root = new GameObject("Staredown Meta Avatar Runtime");
            root.SetActive(false);
            try
            {
                var skinning = root.AddComponent<GpuSkinningConfiguration>();
                var skin = new SerializedObject(skinning);
                Assign(skin, "_CombineMorphTargetsShader", Load<Shader>("Scripts/Skinning/GpuSkinning/Shaders/combine-morph-targets.shader"));
                Assign(skin, "_SkinToTextureShader", Load<Shader>("Scripts/Skinning/GpuSkinning/Shaders/skin-to-texture.shader"));
                Assign(skin, "_morphAndSkinningComputeShader", Load<ComputeShader>("Scripts/Skinning/GpuSkinning/Shaders/OvrApplyMorphsAndSkinning.compute"));
                var lods = skin.FindProperty("QualityPerLOD");
                lods.arraySize = 5;
                for (int i = 0; i < lods.arraySize; i++) lods.GetArrayElementAtIndex(i).intValue = 4;
                skin.FindProperty("MotionSmoothing").boolValue = true;
                skin.ApplyModifiedPropertiesWithoutUndo();

                var shaders = root.AddComponent<OvrAvatarShaderManagerSingle>();
                var serializedShaders = new SerializedObject(shaders);
                Assign(serializedShaders, "DefaultShaderConfigurationInitializer", full);
                Assign(serializedShaders, "FastLoadConfigurationInitializer", vertex);
                Assign(serializedShaders, "CelShaderConfigurationInitializer", cel);
                serializedShaders.ApplyModifiedPropertiesWithoutUndo();

                var lod = root.AddComponent<AvatarLODManager>();
                lod.cullingDisablesParentGameObject = false;
                lod.maxActiveAvatars = 20;
                lod.maxVerticesToSkin = 80000;
                lod.MinLodLevel = 0;
                lod.MaxLodLevel = 4;

                var manager = root.AddComponent<OvrAvatarManager>();
                manager.ShaderManager = shaders;
                manager.automaticallyRequestPermissions = false;
                var serializedManager = new SerializedObject(manager);
                serializedManager.FindProperty("_preloadZipFiles").arraySize = 0;
                serializedManager.ApplyModifiedPropertiesWithoutUndo();
                config.ManagerPrefab = PrefabUtility.SaveAsPrefabAsset(root, ManagerPath);
            }
            finally { DestroyImmediate(root); }
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(ManagerPath);
        }

        static OvrAvatarShaderConfiguration CopyLightingConfig(string sourceName, string destinationName)
        {
            string destination = Folder + "/" + destinationName + ".asset";
            var result = AssetDatabase.LoadAssetAtPath<OvrAvatarShaderConfiguration>(destination);
            if (result == null)
            {
                result = Instantiate(Load<OvrAvatarShaderConfiguration>("Scripts/Common/Shaders/Configurations/Recommended/" + sourceName + ".asset"));
                result.name = destinationName;
                // Use the arena's ambient probes and directional light instead of unassigned SDK IBL cubemaps.
                for (int i = 0; i < result.KeywordsToEnable.Length; i++)
                    if (result.KeywordsToEnable[i] == "LIGHTING_MODE_IBL_PLUS_PUNCTUAL") result.KeywordsToEnable[i] = "LIGHTING_MODE_SH_PLUS_PUNCTUAL";
                result.OnEnable();
                AssetDatabase.CreateAsset(result, destination);
            }
            return result;
        }

        static T Load<T>(string relative) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(Package + relative);
            if (asset == null) throw new InvalidOperationException("SDK asset missing: " + relative);
            return asset;
        }

        static void Assign(SerializedObject target, string property, UnityEngine.Object value)
        {
            var field = target.FindProperty(property);
            if (field == null) throw new InvalidOperationException("SDK field missing: " + property);
            field.objectReferenceValue = value;
        }
    }
}
