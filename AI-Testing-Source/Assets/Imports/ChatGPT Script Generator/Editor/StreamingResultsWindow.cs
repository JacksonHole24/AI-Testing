using System;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;

namespace ScriptGenerator {
public sealed class StreamingResultsWindow : EditorWindow {
    private const int WindowWidth = 400;
    private const int WindowHeight = 400;

    private string _code = "";
    private string _className = "";
    private string _footer = "";
    private Action _cancelCallback;
    private bool _isComplete;

    [CanBeNull]
    private GameObject _targetGameObject;
    [CanBeNull]
    private DefaultAsset _targetFolder;
    [CanBeNull]
    private MonoScript _targetScript;

    public string TargetFolder => AssetDatabase.GetAssetPath(_targetFolder);

    private void OnEnable() => AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
    void OnDisable() => AssemblyReloadEvents.afterAssemblyReload -= OnAfterAssemblyReload;

    public void Initialize(Action cancelCallback, GameObject targetGameObject, DefaultAsset targetFolder,
                           MonoScript targetScript) {
        _cancelCallback = cancelCallback;
        _isComplete = false;
        _className = "";
        _targetGameObject = targetGameObject;
        _targetFolder = targetFolder;
        _targetScript = targetScript;

        var size = new Vector2(WindowWidth, WindowHeight);
        position = new Rect(Screen.currentResolution.width / 2f - size.x / 2f,
                            Screen.currentResolution.height / 2f - size.y / 2f, size.x, size.y);
        Repaint();
    }

    void OnAfterAssemblyReload() {
        Close();

        if (_targetGameObject != null) {
            AddScriptToGameObject();
        }
    }

    private void OnDestroy() {
        if (!_isComplete) {
            _cancelCallback?.Invoke();
        }
    }

    private void OnGUI() {
        GUI.skin.verticalScrollbar.fixedWidth = 0;
        var scrollPosition = new Vector2(0, float.PositiveInfinity);
        EditorGUILayout.BeginScrollView(scrollPosition);
        var style = new GUIStyle(EditorStyles.textArea) { wordWrap = true };
        EditorGUILayout.TextArea(_code, style, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();

        EditorGUILayout.LabelField(_isComplete ? _footer : "Generating script...",
                                   new GUIStyle(EditorStyles.wordWrappedLabel) { richText = true });
        if (GUILayout.Button(_isComplete ? "Close" : "Cancel")) {
            _cancelCallback?.Invoke();
            Close();
        }
    }

    public void AddDelta(string delta) {
        _code += delta;
        Repaint();
    }

    public void Complete(string className, string path) {
        _isComplete = true;
        _className = className;
        _footer = _targetScript == null
            ? $"Created class <b>{className}</b> at <b>{path}</b>."
            : $"Replaced script at <b>{path}</b> with class <b>{className}</b>.";
        Repaint();
    }

    private void AddScriptToGameObject() {
        Debug.Assert(_targetGameObject != null, nameof(_targetGameObject) + " != null");
        var script = Resources.FindObjectsOfTypeAll<MonoScript>().FirstOrDefault(s => s.name == _className);
        if (script != null) {
            Undo.RegisterCreatedObjectUndo(_targetGameObject, "ChatGPT Script Generator");
            _targetGameObject.AddComponent(script.GetClass());
        } else {
            Debug.LogError($"<b>[ChatGPT Script Generator]</b> Script <b>{_className}</b> can't be added to " +
                           $"<b>{_targetGameObject.name}</b> GameObject. Please check any errors in the Console.");
        }
    }
}
}