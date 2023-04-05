using UnityEngine;
using UnityEditor;
using JetBrains.Annotations;

namespace ScriptGenerator {
internal sealed class PromptWindow : EditorWindow {
    private const float AdvancedOptionsHeight = 112;

    private string _prompt = "";
    private bool _deletePreviousFile;
    private bool _advancedOptionsExpanded;
    private float _temperature;
    private bool _saveToHistory;
    private int _timeout;

    [CanBeNull]
    private GameObject _targetGameObject;
    [CanBeNull]
    private DefaultAsset _targetFolder;
    [CanBeNull]
    private MonoScript _targetScript;

    public static PromptWindow ShowWindow(GameObject targetGameObject, DefaultAsset targetFolder,
                                          MonoScript targetScript, string title) {
        var window = GetWindow<PromptWindow>(true, title);
        window.Initialize(targetGameObject, targetFolder, targetScript);
        window.position = new Rect(Screen.currentResolution.width / 2f - window.position.width / 2f,
                                   Screen.currentResolution.height / 2f - window.position.height / 2f,
                                   window.position.width, 140);
        window.Show();
        return window;
    }

    private void Initialize([CanBeNull] GameObject targetGameObject, [CanBeNull] DefaultAsset targetFolder,
                            [CanBeNull] MonoScript targetScript) {
        _targetGameObject = targetGameObject;
        _targetFolder = targetFolder;
        _targetScript = targetScript;
        _deletePreviousFile = EditorPrefsService.GetDeletePreviousFile();
        _saveToHistory = EditorPrefsService.GetSaveToHistory();
        _temperature = Settings.instance.temperature;
        _timeout = Settings.instance.useTimeout ? Settings.instance.timeout : 0;
    }

    private bool IsApiKeyOk => !string.IsNullOrEmpty(Settings.instance.apiKey);

    private void OnGUI() {
        if (!IsApiKeyOk) {
            EditorGUILayout.HelpBox("API Key hasn't been set. Please check the project settings.", MessageType.Error);
            if (GUILayout.Button("Open Project Settings")) {
                SettingsService.OpenProjectSettings("Project/ChatGPT Script Generator");
                Close();
            }

            return;
        }

        if (string.IsNullOrEmpty(_prompt)) {
            _prompt = EditorPrefsService.GetPrompt(_targetGameObject, _targetScript);
        }

        _prompt = EditorGUILayout.TextArea(_prompt, new GUIStyle(EditorStyles.textArea) { wordWrap = true },
                                           GUILayout.ExpandHeight(true));

        var previousClassName = EditorPrefsService.GetClassName();

        // Delete previous file
        {
            var deletePreviousFileContent = new GUIContent($"Overwrite {previousClassName}",
                                                           $"Enable this option to overwrite the previous script named " +
                                                           $"{previousClassName}.");
            _deletePreviousFile =
                !(string.IsNullOrEmpty(previousClassName) || !Selection.activeGameObject ||
                  !Selection.activeGameObject.GetComponent(previousClassName)) &&
                EditorGUILayout.Toggle(deletePreviousFileContent, _deletePreviousFile);
            EditorPrefsService.SetDeletePreviousFile(_deletePreviousFile);
        }

        // Advanced options
        {
            var advancedOptionsContent =
                new GUIContent("Advanced Options", "Override the default script generation settings.");
            var expanded = EditorGUILayout.Foldout(_advancedOptionsExpanded, advancedOptionsContent);
            if (expanded) {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                {
                    // Info
                    {
                        var style = new GUIStyle(EditorStyles.miniLabel) { richText = true };
                        if (_targetGameObject != null) {
                            EditorGUILayout
                                .LabelField($"Adding new component to GameObject <b>{_targetGameObject.name}</b>",
                                            style);
                        }

                        if (_targetScript == null) {
                            var path = _targetFolder == null ? Settings.instance.path : _targetFolder.name;
                            EditorGUILayout.LabelField($"Saving script in <b>{path}</b>", style);
                        } else {
                            EditorGUILayout.LabelField($"Editing script <b>{_targetScript.name}</b>", style);
                        }
                    }

                    // Temperature
                    {
                        var tooltip = "The higher the temperature, the more surprising the results. " +
                                      "The lower the temperature, the more repetitive the results.\n" +
                                      "Current default temperature (set in the project settings): " +
                                      $"{Settings.instance.temperature}.";
                        var temperatureContent = new GUIContent("Temperature", tooltip);
                        _temperature = EditorGUILayout.Slider(temperatureContent, _temperature, 0f, 1f);
                    }

                    // Timeout
                    {
                        var tooltip = "The maximum time in seconds to wait for a response from ChatGPT. If ChatGPT " +
                                      "doesn't respond within the timeout, the script generation fails.\n" +
                                      "Current default timeout (set in the project settings): " +
                                      (Settings.instance.useTimeout
                                          ? $"{Settings.instance.timeout} seconds."
                                          : "unlimited.");
                        var timeoutContent = new GUIContent("Timeout", tooltip);
                        var timeoutEnabled = EditorGUILayout.ToggleLeft(timeoutContent, _timeout > 0);
                        if (timeoutEnabled && _timeout == 0) {
                            _timeout = Settings.instance.timeout;
                        }

                        EditorGUI.indentLevel += 1;
                        _timeout = timeoutEnabled ? EditorGUILayout.IntSlider("Seconds", _timeout, 1, 180) : 0;
                        EditorGUI.indentLevel -= 1;
                    }

                    // Save to history
                    {
                        var tooltip = "Enable this option to save the generated script to the history. " +
                                      "History can be accessed with the button below or in the project settings.";
                        var saveToHistoryContent = new GUIContent("Save to history", tooltip);
                        _saveToHistory = EditorGUILayout.ToggleLeft(saveToHistoryContent, _saveToHistory);
                        EditorPrefsService.SetSaveToHistory(_saveToHistory);
                    }
                }
                EditorGUILayout.EndVertical();

                if (!_advancedOptionsExpanded) {
                    position = new Rect(position.x, position.y - AdvancedOptionsHeight, position.width,
                                        position.height + AdvancedOptionsHeight);
                }

                // Buttons
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("How to write prompts", EditorStyles.miniButton)) {
                        Application.OpenURL(Urls.HowToWritePrompts);
                    }

                    if (GUILayout.Button("Settings", EditorStyles.miniButton)) {
                        SettingsService.OpenProjectSettings("Project/ChatGPT Script Generator");
                    }

                    if (GUILayout.Button("History", EditorStyles.miniButton)) {
                        PromptHistoryWindow.ShowWindow();
                    }

                    EditorGUILayout.EndHorizontal();
                }
            } else {
                if (_advancedOptionsExpanded) {
                    position = new Rect(position.x, position.y + AdvancedOptionsHeight, position.width,
                                        position.height - AdvancedOptionsHeight);
                }
            }

            _advancedOptionsExpanded = expanded;
        }

        // Submit
        {
            var submitContent = new GUIContent(_targetScript == null ? "Generate and Add" : "Generate and Replace",
                                               "Starts the script generation process. " +
                                               "You can also use Ctrl+Enter / Cmd+Enter.");
            // Listen to Ctrl+Enter or Cmd+Enter
            var keyboardTrigger = Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return &&
                                  (Event.current.modifiers & (EventModifiers.Control | EventModifiers.Command)) != 0;
            if (GUILayout.Button(submitContent) || keyboardTrigger) {
                HandleSubmit(previousClassName);
            }
        }
    }

    private void HandleSubmit(string previousClassName) {
        EditorPrefsService.SetPrompt(_prompt, _targetGameObject, _targetScript);

        if (_deletePreviousFile) {
            var gameObject = Selection.activeGameObject;
            if (gameObject != null) {
                var component = gameObject.GetComponent(previousClassName);
                if (component != null) {
                    DestroyImmediate(component);
                }
            }

            var settings = Settings.instance;
            var file = AssetDatabase.LoadAssetAtPath<MonoScript>($"{settings.path}{previousClassName}.cs");
            if (file != null) {
                AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(file));
            }
        }

        try {
            RunGenerator();
        }
        catch (System.Exception e) {
            Debug.LogError(e);
        }

        Close();
    }

    private void RunGenerator() {
        var wrappedPrompt = PromptBuilder.Compose(_prompt, _targetScript);
        var streamingResultsWindow = GetWindow<StreamingResultsWindow>(true, "ChatGPT is generating your script");
        streamingResultsWindow.Show();

        void OnScriptGenerationCompleted(string resultText) {
            if (string.IsNullOrEmpty(resultText)) {
                Close();
                return;
            }

            if (!FixCode(ref resultText)) {
                var message = "ChatGPT did not generate valid code. " +
                              "Please try again with a different prompt or a different temperature.";
                EditorUtility.DisplayDialog("ChatGPT", message, "OK");
                return;
            }

            ScriptAsset.Create(resultText, _targetScript, streamingResultsWindow);
        }

        var cancelCallback = ChatGptService.GenerateScript(wrappedPrompt, _temperature, _timeout,
                                                           delta => streamingResultsWindow.AddDelta(delta),
                                                           OnScriptGenerationCompleted,
                                                           failureCallback: () => streamingResultsWindow.Close());
        streamingResultsWindow.Initialize(cancelCallback, _targetGameObject, _targetFolder, _targetScript);

        if (_saveToHistory) {
            PromptHistory.Add(_prompt, _temperature, _targetGameObject, _targetFolder, _targetScript);
        }
    }

    private bool FixCode(ref string code) {
        code = code.Trim();
        code = code.Trim('\n');
        code = code.Trim('`');
        code = code.Trim('\n');

        var validCodeStart = code.StartsWith("using") || code.StartsWith("namespace") || code.StartsWith("public") ||
                             code.StartsWith("private") || code.StartsWith("protected") ||
                             code.StartsWith("internal") || code.StartsWith("#");
        var validCodeEnd = code.EndsWith("}");

        if (!validCodeStart || !validCodeEnd) {
            return false;
        }

        code += "\n";
        return true;
    }

    void OnEnable() => AssemblyReloadEvents.afterAssemblyReload += Close;
    void OnDisable() => AssemblyReloadEvents.afterAssemblyReload -= Close;
}
}