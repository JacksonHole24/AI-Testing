using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ScriptGenerator {
internal sealed class PromptHistoryWindow : EditorWindow {
    private Vector2 _scrollPosition;
    private readonly HashSet<string> _expandedFoldouts = new HashSet<string>();

    public static void ShowWindow() {
        var window = GetWindow<PromptHistoryWindow>(true, "ChatGPT Script Generator History");
        window.Initialize();
        window.Show();
    }

    private void Initialize() { }

    private void OnGUI() {
        const int columnWidth = 110;
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.Space();
        EditorGUILayout.BeginVertical();

        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, false, false, GUILayout.ExpandWidth(true),
                                                          GUILayout.ExpandHeight(true));

        var prompts = PromptHistory.Get();
        var i = 0;
        foreach (var prompt in prompts) {
            ++i;

            EditorGUILayout.BeginHorizontal();
            var isLatest = i == 1 ? " (latest)" : "";
            EditorGUILayout.LabelField($"#{i}{isLatest}", EditorStyles.boldLabel, GUILayout.Width(columnWidth));
            EditorGUILayout.LabelField(prompt.date, EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Target", EditorStyles.boldLabel, GUILayout.Width(columnWidth));
            var target = string.IsNullOrEmpty(prompt.gameObjectName)
                ? string.IsNullOrEmpty(prompt.targetFolder)
                    ? string.IsNullOrEmpty(prompt.editedScriptName)
                        ? "N/A"
                        : $"Script <b>{prompt.editedScriptName}</b> in <b>{prompt.editedScriptPath}</b>"
                    : $"Folder <b>{prompt.targetFolder}</b>"
                : $"<b>{prompt.gameObjectName}</b> in scene <b>{prompt.sceneName}</b>";
            EditorGUILayout.LabelField(target + ".",
                                       new GUIStyle(EditorStyles.label) { wordWrap = true, richText = true },
                                       GUILayout.ExpandWidth(true));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Guiding Prompts", EditorStyles.boldLabel, GUILayout.Width(columnWidth));
            EditorGUILayout.BeginVertical();
            var wasExpanded = _expandedFoldouts.Contains(prompt.date);
            var isExpanded =
                EditorGUILayout.Foldout(wasExpanded, $"{prompt.guidingPrompts.Length} guiding prompts", true);
            if (isExpanded) {
                _expandedFoldouts.Add(prompt.date);

                foreach (var guidingPrompt in prompt.guidingPrompts) {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("", GUILayout.Width(18));
                    EditorGUILayout.SelectableLabel(guidingPrompt,
                                                    new GUIStyle(EditorStyles.textField) { wordWrap = true },
                                                    GUILayout.ExpandWidth(true),
                                                    GUILayout.Height(EditorGUIUtility.singleLineHeight));
                    if (GUILayout.Button("Copy", EditorStyles.miniButton, GUILayout.Width(50))) {
                        EditorGUIUtility.systemCopyBuffer = guidingPrompt;
                    }

                    EditorGUILayout.EndHorizontal();
                }
            } else {
                if (wasExpanded) {
                    _expandedFoldouts.Remove(prompt.date);
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Temperature", EditorStyles.boldLabel, GUILayout.Width(columnWidth));
            EditorGUILayout.LabelField(prompt.temperature.ToString("0.00"));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Prompt", EditorStyles.boldLabel, GUILayout.Width(columnWidth));
            EditorGUILayout.SelectableLabel(prompt.text, new GUIStyle(EditorStyles.textField) { wordWrap = true },
                                            GUILayout.Height(60), GUILayout.ExpandWidth(true));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Copy", EditorStyles.miniButton, GUILayout.Width(50))) {
                EditorGUIUtility.systemCopyBuffer = prompt.text;
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        }

        // If there is no history, show a message.
        if (prompts.Length == 0) {
            EditorGUILayout.HelpBox("No history yet. Generate some scripts to see them here.", MessageType.Info);
        }

        // If there is history, show a button to clear it.
        if (prompts.Length > 0) {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Clear History")) {
                PromptHistory.Clear();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space();
        EditorGUILayout.EndHorizontal();
    }
}
}