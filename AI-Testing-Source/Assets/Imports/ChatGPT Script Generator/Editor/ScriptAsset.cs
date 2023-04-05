using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ScriptGenerator {
internal static class ScriptAsset {
    public static void Create(string code, MonoScript replaceScript, StreamingResultsWindow streamingResultsWindow) {
        // Extract the class name from the code. It is the last word before the colon.
        var everythingBeforeColon = code.Split(':')[0];
        var words = everythingBeforeColon.Split(' ');
        words = words.Where(w => !string.IsNullOrEmpty(w)).ToArray();
        var className = words[words.Length - 1];

        var path = "";
        if (replaceScript == null) {
            // If the class name already exists, add a number to the end.
            var i = 1;
            while (Resources.FindObjectsOfTypeAll<MonoScript>().Any(s => s.name == className)) {
                className = words[words.Length - 1] + i++;
            }

            // Replace the class name in the code.
            code = code.Replace("class " + words[words.Length - 1], "class " + className);

            EditorPrefsService.SetClassName(className);

            var settings = Settings.instance;
            var folder = string.IsNullOrEmpty(streamingResultsWindow.TargetFolder)
                ? settings.path
                : streamingResultsWindow.TargetFolder;
            if (!AssetDatabase.IsValidFolder(folder)) {
                var folderToCreate = folder.Substring("Assets/".Length);
                var folders = folderToCreate.Split('/');
                var root = "Assets";
                foreach (var subFolder in folders) {
                    root = $"{root}/{subFolder}";
                    if (!AssetDatabase.IsValidFolder(root)) {
                        AssetDatabase.CreateFolder(root.Substring(0, root.LastIndexOf('/')), subFolder);
                    }
                }
            }

            path = $"{folder}/{className}.cs";
        } else {
            path = AssetDatabase.GetAssetPath(replaceScript);
            Debug.Assert(path != null, nameof(path) + " != null");
        }

        Debug.Assert(!string.IsNullOrEmpty(path), "!string.IsNullOrEmpty(path)");
        streamingResultsWindow.Complete(className, path);

        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        var method = typeof(ProjectWindowUtil).GetMethod("CreateScriptAssetWithContent", flags);
        if (method != null) method.Invoke(null, new object[] { path, code });
    }

    public static string Load(MonoScript scriptAsset) {
        Debug.Assert(scriptAsset != null, nameof(scriptAsset) + " != null");
        var path = AssetDatabase.GetAssetPath(scriptAsset);
        Debug.Assert(path != null, nameof(path) + " != null");
        return AssetDatabase.LoadAssetAtPath<MonoScript>(path).text;
    }
}
}