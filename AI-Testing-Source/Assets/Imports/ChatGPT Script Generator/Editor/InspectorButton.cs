using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScriptGenerator {
[InitializeOnLoad]
internal sealed class InspectorButton : Editor {
    private const double UpdateInterval = 0.1f;

    private static readonly Dictionary<EditorWindow, Button> Buttons = new Dictionary<EditorWindow, Button>();
    private static double _lastUpdate;

    private static bool ButtonVisibility => Selection.activeGameObject != null && Settings.instance.showInspectorButton;

    static InspectorButton() {
        EditorApplication.update += EditorUpdate;

        Selection.selectionChanged += () => {
            foreach (var button in Buttons.Values) {
                button.visible = ButtonVisibility;
            }
        };
    }

    private static void EditorUpdate() {
        if (EditorApplication.timeSinceStartup - _lastUpdate < UpdateInterval) return;
        _lastUpdate = EditorApplication.timeSinceStartup;

        var windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
        foreach (var window in windows) {
            if (window.GetType().Name != "InspectorWindow") continue;
            if (Buttons.ContainsKey(window)) continue;

            var buttons = window.rootVisualElement.Q(className: "unity-inspector-add-component-button");
            if (buttons == null) continue;

            var container = new VisualElement {
                style = { flexDirection = FlexDirection.Row, justifyContent = Justify.Center, marginTop = -10 }
            };
            buttons.parent.Add(container);
            var button = new Button(ClickEvent) {
                text = "Generate Component",
                style = { width = 230, height = 25, marginLeft = 2, marginRight = 2 },
                visible = ButtonVisibility
            };
            container.Add(button);
            Buttons.Add(window, button);
        }
    }

    private static void ClickEvent() {
        var window = PromptWindow.ShowWindow(Selection.activeGameObject, null, null, "Generate Component");
        var sourceButton = Buttons.Values.OrderBy(b => (b.worldBound.center - Event.current.mousePosition).sqrMagnitude)
            .First();
        var inspector = Buttons.FirstOrDefault(kvp => kvp.Value == sourceButton).Key;
        if (inspector != null) {
            var rect = new Rect(inspector.position.x, sourceButton.worldBound.yMax + 140, inspector.position.width,
                                window.position.height);
            if (rect.xMax > Screen.currentResolution.width) {
                rect.x = Screen.currentResolution.width - rect.width;
            }

            const float extraBottomSpace = 60;
            if (rect.yMax > Screen.currentResolution.height - extraBottomSpace) {
                rect.y = Screen.currentResolution.height - rect.height - extraBottomSpace;
            }

            window.position = rect;
        }
    }
}
}