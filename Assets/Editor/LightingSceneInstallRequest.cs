using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Explicit one-shot request used for installation without reloading an open/dirty scene.
[InitializeOnLoad]
public static class LightingSceneInstallRequest
{
    public const string RequestPath = "Library/LightingInstall.request";
    public const string ResultPath = "Library/LightingInstall.result";
    static LightingSceneInstallRequest() => EditorApplication.delayCall += Process;
    private static void Process()
    {
        if (!File.Exists(RequestPath)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        { EditorApplication.delayCall += Process; return; }
        string path = File.ReadAllText(RequestPath).Trim();
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before installation.");
            var scene=SceneManager.GetSceneByPath(path);
            if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("Requested scene must already be open; it will not be reloaded automatically.");
            var controller=LightingConfigurationWindow.Install(scene);
            if (!controller) throw new InvalidOperationException("Scene lighting installation failed.");
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save scene.");
            File.WriteAllText(ResultPath,"Installed and saved: "+path+"\nController: "+controller.name);
            LightingConfigurationWindow.Open();
        }
        catch(Exception exception) { File.WriteAllText(ResultPath,"ERROR: "+exception.Message); Debug.LogException(exception); }
        finally { File.Delete(RequestPath); }
    }
}
