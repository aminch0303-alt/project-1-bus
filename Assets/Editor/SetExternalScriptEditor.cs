using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class SetExternalScriptEditor
{
    static SetExternalScriptEditor()
    {
        Apply();
    }

    [MenuItem("Tools/Antigravity/Set as Default Script Editor")]
    public static void Apply()
    {
        string idePath = @"C:\Users\M3 Gaming\AppData\Local\Programs\Antigravity IDE\Antigravity IDE.exe";
        EditorPrefs.SetString("kScriptsDefaultApp", idePath);
        EditorPrefs.SetString("kScriptEditorArgs", "-g \"$(File)\":$(Line)");
        EditorPrefs.SetString("RecentlyUsedScriptApp0", idePath);
        Debug.Log("<color=green>[Preferences]</color> External Script Editor set to: " + idePath);
    }
}

