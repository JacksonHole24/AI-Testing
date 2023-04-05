using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace ScriptGenerator {
#if UNITY_2020_1_OR_NEWER
[FilePath("UserSettings/ChatGptScriptGeneratorSettings.asset", FilePathAttribute.Location.ProjectFolder)]
#endif
public sealed class Settings : ScriptableSingleton<Settings> {
    public enum Model {
        Gpt35Turbo = 0,
        Gpt4 = 1,
    }

    public string apiKey;
    public Model model = Model.Gpt35Turbo;
    public float temperature = 0f;
    public bool useTimeout = true;
    public int timeout = 90;

    public List<string> guidingPrompts;
    public List<bool> guidingPromptsDisabled;

    public string path = "Assets/Scripts/";
    public bool showInspectorButton = true;

    public Settings() {
        ResetGuidingPrompts();
    }

    public void Save() => Save(true);

    void OnDisable() => Save();

    public void ResetGuidingPrompts() {
        guidingPrompts = new List<string> {
            "The script is a component of a GameObject.", //
            "Include all the necessary imports.", // 
            "If using a Unity component, require it with an attribute.", //
            "The components works by itself, without any other custom scripts.", //
            "Generate tooltips for all properties.", // 
            "All properties should have default values.", //
            "Add explanatory comments to the script.", //
            "I only need the script body. Don’t add any explanation.", //
        };
        guidingPromptsDisabled = guidingPrompts.Select(_ => false).ToList();
    }

    public static string GetModelName(Model model) {
        switch (model) {
            case Model.Gpt35Turbo:
                return "gpt-3.5-turbo";
            case Model.Gpt4:
                return "gpt-4";
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public List<string> GetEnabledGuidingPrompts() {
        return guidingPrompts.Where((_, i) => !instance.guidingPromptsDisabled[i]).ToList();
    }
}

sealed class ScriptGeneratorSettingsProvider : SettingsProvider {
    private ReorderableList _guidingPromptsList;

    private ScriptGeneratorSettingsProvider() : base("Project/ChatGPT Script Generator", SettingsScope.Project) { }

    public override void OnGUI(string search) {
        var settings = Settings.instance;
        var userEmail = CloudProjectSettings.userName;
        if (string.IsNullOrEmpty(userEmail)) {
            EditorGUILayout.HelpBox("You must be signed in to Unity to use this feature.", MessageType.Error);
            return;
        }

        var key = string.IsNullOrEmpty(settings.apiKey) ? "" : Cipher.Reveal(settings.apiKey, userEmail);
        var model = settings.model;
        var temperature = settings.temperature;
        var useTimeout = settings.useTimeout;
        var timeout = settings.timeout;
        var path = settings.path;
        var showInspectorButton = settings.showInspectorButton;

        var indentLevel = EditorGUI.indentLevel;
        EditorGUI.BeginChangeCheck();

        // API Key
        {
            EditorGUILayout.LabelField("OpenAI API Key", EditorStyles.boldLabel);
            EditorGUI.indentLevel = indentLevel + 1;

            key = EditorGUILayout.TextField("API Key", key);

            if (string.IsNullOrEmpty(key)) {
                EditorGUILayout.HelpBox("Get an API key from the OpenAI website:\n" + //
                                        "  1. Sign up or log in to your OpenAI account using the button below.\n" + // 
                                        "  2. Navigate to the 'View API Keys' section in your account dashboard.\n" + //
                                        "  3. Click 'Create new secret key' and copy the generated key.",
                                        MessageType.Info);
                if (GUILayout.Button("View API Keys", GUILayout.ExpandHeight(false))) {
                    Application.OpenURL("https://platform.openai.com/account/api-keys");
                }
            }

            EditorGUILayout.HelpBox("The API key is stored in the following file: " + //
                                    "UserSettings/ChatGptScriptGeneratorSettings.asset. \n" + //
                                    "When sharing your project with others, be sure to exclude the 'UserSettings' " + //
                                    "directory to prevent unauthorized use of your API key.", MessageType.Info, false);

            EditorGUI.indentLevel = indentLevel;
        }

        EditorGUILayout.Space(15);

        // Model selection
        {
            EditorGUILayout.LabelField("GPT Model", EditorStyles.boldLabel);
            EditorGUI.indentLevel = indentLevel + 1;

            EditorGUILayout.BeginHorizontal();
            var modelNames = new string[Enum.GetNames(typeof(Settings.Model)).Length];
            for (var i = 0; i < modelNames.Length; i++) {
                modelNames[i] = Settings.GetModelName((Settings.Model)i);
            }

            model = (Settings.Model)EditorGUILayout.Popup("Model", (int)model, modelNames, GUILayout.MaxWidth(300));
            if (GUILayout.Button("OpenAI Documentation", GUILayout.MaxWidth(200))) {
                Application.OpenURL("https://platform.openai.com/docs/models");
            }

            EditorGUILayout.EndHorizontal();

            if (model == Settings.Model.Gpt4) {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.HelpBox("The GPT-4 model is currently in a limited beta and only accessible to " + //
                                        "those who have been granted access. You can join the waitlist to get " + //
                                        "access when capacity is available.", MessageType.Warning, false);
                // GUILayoutUtility.GetLastRect().height is 1 for some reason, so we have to hardcode the height.
                if (GUILayout.Button("OpenAI GPT-4 Waitlist", GUILayout.MaxHeight(38), GUILayout.MaxWidth(300 - 5),
                                     GUILayout.MinWidth(50))) {
                    Application.OpenURL("https://openai.com/waitlist/gpt-4");
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUI.indentLevel = indentLevel;
        }

        EditorGUILayout.Space(10);

        EditorGUILayout.LabelField("Generation Settings", EditorStyles.boldLabel);
        EditorGUI.indentLevel = indentLevel + 1;

        // Temperature
        {
            temperature = EditorGUILayout.Slider("Default temperature", temperature, 0.0f, 1.0f);
            const string m = "Temperature controls randomness: Lower values result in less random results. " + //
                             "As the temperature approaches zero, the model becomes deterministic and " + //
                             "repetitive. Higher values result in more random results.";
            EditorGUILayout.HelpBox(m, MessageType.Info, false);
        }

        EditorGUILayout.Space(10);

        // Guiding prompts
        {
            if (_guidingPromptsList == null) {
                _guidingPromptsList =
                    new ReorderableList(settings.guidingPrompts, typeof(string), true, true, true, true) {
                        drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Guiding Prompts"),
                        // ReSharper disable UnusedParameter.Local
                        drawElementCallback = (rect, index, isActive, isFocused) => {
                            if (index >= settings.guidingPrompts.Count) return;
                            if (settings.guidingPromptsDisabled.Count <= index) {
                                settings.guidingPromptsDisabled.Add(false);
                            }

                            if (settings.guidingPromptsDisabled[index]) {
                                EditorGUI.BeginDisabledGroup(true);
                            }

                            var element = settings.guidingPrompts[index];
                            rect.height = EditorGUIUtility.singleLineHeight + 2;
                            EditorGUILayout.BeginHorizontal();

                            // Text field
                            var textFieldRect = new Rect(rect.x, rect.y, rect.width - 45, rect.height);
                            settings.guidingPrompts[index] = EditorGUI.TextArea(textFieldRect, element);
                            if (_guidingPromptsList != null) {
                                _guidingPromptsList.list = settings.guidingPrompts;
                            }

                            // Disabled toggle
                            if (settings.guidingPromptsDisabled[index]) {
                                EditorGUI.EndDisabledGroup();
                            }

                            var disabled = settings.guidingPromptsDisabled[index];
                            var toggleRect = new Rect(rect.x + rect.width - 40, rect.y, 20, rect.height);
                            settings.guidingPromptsDisabled[index] = !EditorGUI.Toggle(toggleRect, !disabled);

                            // Remove button
                            var binIcon = EditorGUIUtility.IconContent("TreeEditor.Trash");
                            if (GUI.Button(new Rect(rect.x + rect.width - 20, rect.y + 1, 20, rect.height), binIcon,
                                           new GUIStyle(EditorStyles.miniButton) {
                                               alignment = TextAnchor.MiddleCenter,
                                               padding = new RectOffset(0, 0, 0, 0),
                                           })) {
                                settings.guidingPrompts.RemoveAt(index);
                                if (_guidingPromptsList != null) {
                                    _guidingPromptsList.list = settings.guidingPrompts;
                                }
                            }

                            EditorGUILayout.EndHorizontal();
                        },
                        elementHeight = EditorGUIUtility.singleLineHeight + 5,
                    };
            }

            // Indent level doesn't work for ReorderableList, so we have to do it manually.
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(18);
            _guidingPromptsList.DoLayoutList();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(18);
            var indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            const string guidingPromptsHelp = "Guiding prompts are used to provide context for the model. " + //
                                              "Provided prompts are used to generate each response.";
            EditorGUILayout.HelpBox(guidingPromptsHelp, MessageType.Info);
            EditorGUI.indentLevel = indent;

            if (GUILayout.Button("Reset to default", GUILayout.MaxWidth(300))) {
                settings.ResetGuidingPrompts();
                _guidingPromptsList.list = settings.guidingPrompts;
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(10);

        // Timeout
        {
            useTimeout = EditorGUILayout.Toggle("Timeout", useTimeout);

            if (useTimeout) {
                EditorGUI.indentLevel++;
                timeout = EditorGUILayout.IntField("Duration (seconds)", timeout);
                if (timeout < 1) timeout = 1;
                EditorGUI.indentLevel--;
            }
        }

        EditorGUI.indentLevel = indentLevel;
        EditorGUILayout.Space(10);

        EditorGUILayout.LabelField("General", EditorStyles.boldLabel);
        EditorGUI.indentLevel = indentLevel + 1;

        // Output path
        {
            var content = new GUIContent("Output Path", "The path where the generated script will be saved. " + //
                                                        "The path must start with 'Assets/'.");
            path = EditorGUILayout.TextField(content, path);
            if (path.EndsWith("/")) path = path.Substring(0, path.Length - 1);
            if (!path.StartsWith("Assets/")) path = "Assets/" + path;
        }

        // Inspector button
        {
            var content = new GUIContent("Inspector button",
                                         "If enabled, 'Generate Component' button is shown in the inspector below " +
                                         "the 'Add Component' button.");
            showInspectorButton = EditorGUILayout.Toggle(content, showInspectorButton);
        }

        EditorGUI.indentLevel = indentLevel;

        if (EditorGUI.EndChangeCheck()) {
            settings.apiKey = string.IsNullOrEmpty(key) ? "" : Cipher.Hide(key, userEmail);
            settings.model = model;
            settings.temperature = temperature;
            settings.useTimeout = useTimeout;
            settings.timeout = timeout;
            settings.path = path;
            settings.showInspectorButton = showInspectorButton;
            settings.Save();
        }

        // Prompt history
        {
            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Prompt History", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(18);
            EditorGUILayout.BeginVertical();
            EditorGUILayout.HelpBox("View the history of prompts you've used to generate scripts.", MessageType.Info);

            if (GUILayout.Button("View History")) {
                PromptHistoryWindow.ShowWindow();
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        // Buttons
        {
            EditorGUILayout.LabelField("Useful Links", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("OpenAI Status Page")) {
                Application.OpenURL(Urls.OpenAiStatus);
            }

            if (GUILayout.Button("ChatGPT Playground")) {
                Application.OpenURL(Urls.OpenAiPlayground);
            }

            if (GUILayout.Button("How to write prompts")) {
                Application.OpenURL(Urls.HowToWritePrompts);
            }

            if (GUILayout.Button("Discord")) {
                Application.OpenURL(Urls.Discord);
            }

            if (GUILayout.Button("Asset Store Page")) {
                Application.OpenURL(Urls.AssetStorePage);
            }

            EditorGUILayout.EndHorizontal();
        }
    }

    [SettingsProvider]
    public static SettingsProvider CreateCustomSettingsProvider() => new ScriptGeneratorSettingsProvider();
}
} // namespace AICommand