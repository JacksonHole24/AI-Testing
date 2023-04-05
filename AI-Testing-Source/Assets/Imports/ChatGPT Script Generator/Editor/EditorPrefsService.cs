using UnityEditor;
using UnityEngine;

namespace ScriptGenerator {
internal static class EditorPrefsService {
    public static void SetClassName(string className) {
        EditorPrefs.SetString("ChatGptScriptGeneratorClassName", className);
    }

    public static string GetClassName() {
        return EditorPrefs.GetString("ChatGptScriptGeneratorClassName", "");
    }

    public static void SetPrompt(string prompt, GameObject targetGameObject, MonoScript targetScript) {
        var key = targetGameObject != null ? $"ChatGptScriptGeneratorPrompt-{targetGameObject.GetInstanceID()}" :
            targetScript != null ? "ChatGptScriptGeneratorPrompt-Edit" : "ChatGptScriptGeneratorPrompt-Generic";
        EditorPrefs.SetString(key, prompt);
    }

    public static string GetPrompt(GameObject targetGameObject, MonoScript targetScript) {
        var key = targetGameObject != null ? $"ChatGptScriptGeneratorPrompt-{targetGameObject.GetInstanceID()}" :
            targetScript != null ? "ChatGptScriptGeneratorPrompt-Edit" : "ChatGptScriptGeneratorPrompt-Generic";
        return EditorPrefs.GetString(key, "");
    }

    public static bool GetDeletePreviousFile() {
        return EditorPrefs.GetBool("ChatGptScriptGeneratorDeletePreviousFile", true);
    }

    public static void SetDeletePreviousFile(bool value) {
        EditorPrefs.SetBool("ChatGptScriptGeneratorDeletePreviousFile", value);
    }

    public static bool GetSaveToHistory() {
        return EditorPrefs.GetBool("ChatGptScriptGeneratorSaveToHistory", true);
    }

    public static void SetSaveToHistory(bool value) {
        EditorPrefs.SetBool("ChatGptScriptGeneratorSaveToHistory", value);
    }
}
}