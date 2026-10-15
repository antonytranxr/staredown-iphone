using System;
using System.Threading.Tasks;
using MCPForUnity.Editor.Services;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class CodexBridgeStartup
{
    static CodexBridgeStartup()
    {
        if (!Application.isBatchMode)
            EditorApplication.update += OnReady;
    }

    private static void OnReady()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        EditorApplication.update -= OnReady;
        Start();
    }

    [MenuItem("Tools/Codex/Start Editor Connection")]
    public static async void Start()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.update += OnReady;
            return;
        }
        try
        {
            Debug.Log("Codex: starting editor connection.");
            EditorPrefs.SetString("MCPForUnity.UvxPath", "/Users/Antony/Documents/Codex/tools/uv-python/bin/uvx");
            EditorPrefs.SetBool("MCPForUnity.UseHttpTransport", true);
            EditorPrefs.SetString("MCPForUnity.HttpTransportScope", "local");
            EditorPrefs.SetString("MCPForUnity.HttpUrl", "http://localhost:8080");
            EditorPrefs.SetBool("MCPForUnity.AutoStartOnLoad", false);
            var server = MCPServiceLocator.Server;
            if (!server.IsLocalHttpServerReachable() && !server.StartLocalHttpServer(quiet: true))
            {
                Debug.LogWarning("Codex: server launch failed. Open Window > MCP for Unity for diagnostics.");
                return;
            }
            for (int attempt = 0; attempt < 120; attempt++)
            {
                if (server.IsLocalHttpServerReachable())
                {
                    bool connected = await MCPServiceLocator.Bridge.StartAsync();
                    Debug.Log("Codex: editor bridge connected = " + connected);
                    return;
                }
                await Task.Delay(1000);
            }
            Debug.LogWarning("Codex: server startup timed out. Use Tools > Codex > Start Editor Connection to retry.");
        }
        catch (Exception exception) { Debug.LogException(exception); }
    }
}
