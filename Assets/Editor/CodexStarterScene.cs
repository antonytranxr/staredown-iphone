using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class CodexStarterScene
{
    private const string ScenePath = "Assets/Scenes/VRStarter.unity";
    static CodexStarterScene() { EditorApplication.update += OnReady; }
    private static void OnReady()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        EditorApplication.update -= OnReady;
        CreateIfMissing();
    }

    private static void CreateIfMissing()
    {
        if (Application.isBatchMode || File.Exists(ScenePath)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += CreateIfMissing;
            return;
        }
        var current = EditorSceneManager.GetActiveScene();
        if (current.isDirty || !string.IsNullOrEmpty(current.path)) return;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.meta.xr.sdk.core/Prefabs/OVRCameraRig.prefab");
        if (prefab == null) { Debug.LogError("Codex: OVRCameraRig prefab is missing."); return; }
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        if (Camera.main != null) Object.DestroyImmediate(Camera.main.gameObject);
        PrefabUtility.InstantiatePrefab(prefab, scene);
        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Floor";
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "XR Test Cube";
        cube.transform.position = new Vector3(0, 1.2f, 2);
        cube.transform.localScale = Vector3.one * 0.3f;
        Directory.CreateDirectory("Assets/Scenes");
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
        {
            Debug.LogError("Codex: starter scene could not be saved.");
            return;
        }
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        Debug.Log("Codex: saved starter scene " + ScenePath);
    }
}
