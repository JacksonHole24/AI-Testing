using UnityEditor;

namespace ScriptGenerator {
public static class CreateAssetMenuItem {
    [MenuItem("Assets/Create/C# Script by ChatGPT", false, 80)]
    public static void GenerateScript() {
        var selectedFolder = Selection.activeObject as DefaultAsset;
        PromptWindow.ShowWindow(null, selectedFolder, null, title: "Generate C# Script");
    }
}
}