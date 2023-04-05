using System;
using UnityEditor;
using UnityEngine;

namespace ScriptGenerator {
public static class ApiErrorHandler {
    public static void HandleError(string error) {
        if (error.Contains("401")) {
            const string m = "Invalid API key. Ensure the correct API key and requesting organization are being " +
                             "used.\n\nThis can also happen if you are logged in to Unity with a different " +
                             "account than the one you used when entering the API key.";
            DisplayDialogComplex("ChatGPT returned error", m, "Open ChatGPT API keys page",
                                 () => Application.OpenURL(Urls.OpenAiApiKeysPage), "Open Project Settings",
                                 () => SettingsService.OpenProjectSettings("Project/ChatGPT Script Generator"));
        } else if (error.Contains("404")) {
            var m = $"ChatGPT returned error:\n{error}\n\n" + //
                    "This error usually happens when you are trying to use a model that is not available. " +
                    "For example, if you are using the GPT-4 model, you will get this error if you have not been " +
                    "given GPT-4 access.\n\n" + //
                    "Please check the model you are using in the ChatGPT Script Generator settings.";
            var action = EditorUtility.DisplayDialog("ChatGPT returned error", m, "Open Project Settings", "OK");
            if (action) {
                SettingsService.OpenProjectSettings("Project/ChatGPT Script Generator");
            }
        } else if (error.Contains("429")) {
            const string m = "If you see this error message, it means your OpenAI free trial is expired or inactive. " +
                             "Please note that the ChatGPT Plus subscriptions do not include paid access to the " +
                             "OpenAI platform.\n\n" + //
                             "In order to check for sure, please go to the OpenAI playground using the button below " +
                             "and try submitting a prompt. If you get an error there, it means your account requires " +
                             "you to set up a paid account in OpenAI dashboard using the button below.";
            DisplayDialogComplex("ChatGPT returned error", m, "Open OpenAI playground",
                                 () => Application.OpenURL(Urls.OpenAiPlayground), "Open OpenAI dashboard",
                                 () => Application.OpenURL(Urls.OpenAiBilling));
        } else if (error.Contains("500")) {
            const string m = "ChatGPT servers returned error 500:\n" + //
                             "\"Internal Server Error\"\n\n" + //
                             "Cause: Issue on ChatGPT servers.\n" + //
                             "Solution: Retry your request after a brief wait, contact us on Discord if the issue persists.";
            DisplayDialogComplex("ChatGPT returned error", m, "Open ChatGPT status page",
                                 () => Application.OpenURL(Urls.OpenAiStatus), "Message us on Discord",
                                 () => Application.OpenURL(Urls.Discord));
        } else {
            var m = $"ChatGPT returned error:\n{error}\n\n" + //
                    "This is often caused by an error on the ChatGPT servers. " +
                    "If this error persists, please contact us on Discord.";
            DisplayDialogComplex("ChatGPT returned error", m, "Message us on Discord",
                                 () => Application.OpenURL(Urls.Discord), "Open OpenAI status page",
                                 () => Application.OpenURL(Urls.OpenAiStatus));
        }
    }

    public static void DisplayDialogComplex(string title, string message, string button1, Action action1,
                                            string button2, Action action2) {
        var dismissIsSecondButton = Application.platform == RuntimePlatform.WindowsEditor;
        int action = EditorUtility.DisplayDialogComplex(title, message, button1, dismissIsSecondButton ? "OK" : button2,
                                                        dismissIsSecondButton ? button2 : "OK");
        switch (action) {
            case 0:
                action1?.Invoke();
                break;
            case 1:
                if (!dismissIsSecondButton) {
                    action2?.Invoke();
                }

                break;
            case 2:
                if (dismissIsSecondButton) {
                    action2?.Invoke();
                }

                break;
        }
    }
}
}