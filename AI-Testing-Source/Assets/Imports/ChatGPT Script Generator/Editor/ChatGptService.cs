using System;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;
using UnityEditor;
using UnityEngine.Networking;

namespace ScriptGenerator {
#pragma warning disable 0649
[Serializable]
public struct Request {
    public string model;
    public RequestMessage[] messages;
    public float temperature;
    public bool stream;
}

[Serializable]
public struct RequestMessage {
    public string role;
    public string content;
}

[Serializable]
public struct Response {
    public string id;
    public ResponseChoice[] choices;
}

[Serializable]
public struct ResponseChoice {
    public int index;
    public ResponseMessage delta;

    // ReSharper disable once InconsistentNaming
    public string finish_reason;
}

[Serializable]
public struct ResponseMessage {
    public string role;
    public string content;
}
#pragma warning restore 0649

[SuppressMessage("ReSharper", "NotAccessedField.Global")]
static class ChatGptService {
    private const string UrlChatCompletions = "https://api.openai.com/v1/chat/completions";
    private static IEnumerator _enumerator;

    public static Action GenerateScript(string prompt, float temperature, int timeout, Action<string> updateCallback,
                                        Action<string> completeCallback, Action failureCallback) {
        var cancelCallback = new Action(() => {
            if (_enumerator != null) {
                EditorApplication.update -= Update;
                _enumerator = null;
            }
        });

        if (_enumerator != null) {
            EditorApplication.update -= Update;
            _enumerator = null;
        }

        _enumerator = Stream(prompt, temperature, timeout, updateCallback, completeCallback, failureCallback);
        EditorApplication.update += Update;

        return cancelCallback;
    }

    private static void Update() {
        if (_enumerator != null && !_enumerator.MoveNext()) {
            EditorApplication.update -= Update;
            _enumerator = null;
        }
    }

    private static IEnumerator Stream(string prompt, float temperature, int timeout, Action<string> updateCallback,
                                      Action<string> completeCallback, Action failureCallback) {
        var settings = Settings.instance;
        var requestObject = new Request {
            model = Settings.GetModelName(settings.model),
            temperature = temperature,
            stream = true,
            messages = new[] { new RequestMessage { role = "user", content = prompt } }
        };

        var requestJson = JsonUtility.ToJson(requestObject);

#if UNITY_2022_2_OR_NEWER
        var request = UnityWebRequest.Post(UrlChatCompletions, requestJson, "application/json");
#else
        var request = new UnityWebRequest(UrlChatCompletions, "POST");
        request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(requestJson));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
#endif

        request.timeout = timeout;

        var userEmail = CloudProjectSettings.userName;
        if (string.IsNullOrEmpty(userEmail)) {
            var m = "[ChatGPT Script Generator] You must be signed in to Unity to use this feature.";
            throw new Exception(m);
        }

        var key = Cipher.Reveal(settings.apiKey, userEmail);
        try {
            request.SetRequestHeader("Authorization", "Bearer " + key);
        }
        catch (Exception e) {
            if (e.Message.ToLower().Contains("header value")) {
                var m = $"{e.Message}. This error most likely means that your API key was entered under a different " +
                        $"Unity account. If that's correct, please re-enter your API Key in the Project Settings or " +
                        $"re-login with the previous Unity account. If the issue persists, please contact us on " +
                        $"Discord or email.";
                ApiErrorHandler.DisplayDialogComplex("Web Request Error", m, "Discord",
                                                     () => Application.OpenURL(Urls.Discord), "Email",
                                                     () => Application.OpenURL(Urls.Email));
            } else {
                Debug.LogError("[ChatGPT] " + e.Message);
            }

            failureCallback();
            yield break;
        }

        var webRequest = request.SendWebRequest();

        int textLength = 0;
        string completeText = "";

        while (!webRequest.isDone) {
            if (request.downloadHandler.text.Length > textLength) {
                if (!string.IsNullOrEmpty(request.error)) {
                    ApiErrorHandler.HandleError(request.error);
                    Debug.LogError("[ChatGPT] " + request.error);
                    failureCallback();
                    yield break;
                }

                var text = request.downloadHandler.text;
                var newText = text.Substring(textLength);
                textLength = text.Length;
                while (newText.Contains("data: ")) {
                    var startTrimmed =
                        newText.Substring(newText.IndexOf("data: ", StringComparison.Ordinal) + "data: ".Length);
                    var dataEndPosition = startTrimmed.IndexOf("data: ", StringComparison.Ordinal);
                    var dataJson = dataEndPosition == -1 ? startTrimmed : startTrimmed.Substring(0, dataEndPosition);
                    newText = dataEndPosition == -1 ? "" : startTrimmed.Substring(dataEndPosition);
                    if (dataJson.Contains("[DONE]")) {
                        break;
                    }

                    try {
                        var data = JsonUtility.FromJson<Response>(dataJson);

                        if (data.choices == null || data.choices.Length == 0) {
                            Debug.LogError("[ChatGPT] No choices received");
                            // c1952588-3bd1-4b78-b16c-038ec6bbc6cd
                            failureCallback();
                            yield break;
                        }

                        if (data.choices[0].finish_reason == "length") {
                            EditorUtility.DisplayDialog("ChatGPT returned error",
                                                        "The script was truncated because it reached the maximum length. " +
                                                        "Please try again with a shorter prompt.", "OK");
                            Debug.LogError("[ChatGPT] Script was truncated");
                            failureCallback();
                            yield break;
                        }

                        var delta = data.choices[0].delta.content;
                        completeText += delta;
                        updateCallback?.Invoke(delta);
                    }
                    catch (Exception e) {
                        Debug.LogError($"[ChatGPT] {e.Message}\n1: {dataJson}; 2: {startTrimmed}; 3: {newText}");
                        failureCallback();
                        yield break;
                    }
                }
            }

            yield return null;
        }

        if (!string.IsNullOrEmpty(request.error)) {
            failureCallback();
            ApiErrorHandler.HandleError(request.error);
            yield break;
        }

        Debug.Assert(request.responseCode == 200, "Request failed with status code: " + request.responseCode);
        Debug.Assert(string.IsNullOrEmpty(request.error), "Request failed with error: " + request.error);

        if (!string.IsNullOrEmpty(completeText)) {
            completeCallback(completeText);
        }

        request.Dispose();
    }
}
}