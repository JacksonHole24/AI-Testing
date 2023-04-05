using UnityEditor;

namespace ScriptGenerator {
public static class ScriptContextMenuItem {
    [MenuItem("Assets/Edit C# Script with ChatGPT", false, 1050)]
    public static void GenerateScript() {
        var selectedScript = Selection.activeObject as MonoScript;
        PromptWindow.ShowWindow(targetGameObject: null, targetFolder: null, targetScript: selectedScript,
                                title: "Edit C# Script");
    }

    [MenuItem("Assets/Edit C# Script with ChatGPT", true)]
    public static bool ValidateGenerateScript() {
        var selection = Selection.activeObject;
        return selection != null && selection is MonoScript;
    }
}
}