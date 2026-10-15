using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Meta's runtime selection lives in the editor process and is lost on restart.
// Reapply it for this project's Play workflow without changing Android settings.
[InitializeOnLoad]
public static class StareSimulatorStartup
{
    const string Menu = "Staredown/Auto-activate XR Simulator on Play";
    static string Preference => "Staredown.AutoSimulator." + Application.dataPath;
    static bool Enabled => EditorPrefs.GetBool(Preference, true);

    static StareSimulatorStartup()
    {
        EditorApplication.delayCall += OnReady;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.ExitingEditMode) Prepare();
        };
    }

    static void OnReady()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += OnReady;
            return;
        }
        if (!EditorApplication.isPlayingOrWillChangePlaymode) Prepare();
    }

    public static void Prepare()
    {
        if (!Enabled || Application.isBatchMode || BuildPipeline.isBuildingPlayer) return;
        // Operator previously caused native crashes on this development Mac.
        if (EditorPrefs.GetBool("Meta.XR.MetaXROperator.Activated", false))
            EditorApplication.ExecuteMenuItem("Window/Meta/Meta XR Operator/Deactivate");

        string runtime = Environment.GetEnvironmentVariable("XR_SELECTED_RUNTIME_JSON");
        if (string.IsNullOrEmpty(runtime) || !File.Exists(runtime) ||
            !string.Equals(Path.GetFileName(runtime), "meta_openxr_simulator.json", StringComparison.OrdinalIgnoreCase))
            EditorApplication.ExecuteMenuItem("Window/Meta/Meta XR Simulator/Activate");
    }

    [MenuItem(Menu)]
    static void Toggle()
    {
        EditorPrefs.SetBool(Preference, !Enabled);
        if (Enabled && !EditorApplication.isPlayingOrWillChangePlaymode) Prepare();
    }

    [MenuItem(Menu, true)]
    static bool Validate()
    {
        UnityEditor.Menu.SetChecked(Menu, Enabled);
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }
}
