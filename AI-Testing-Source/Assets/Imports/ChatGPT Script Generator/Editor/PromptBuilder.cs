using System.Linq;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;

namespace ScriptGenerator {
internal static class PromptBuilder {
    private const string PromptStart = "Write code of a C# Unity script for a given task.\n";

    private const string GuidingPromptsPrefix = "Some requirements for the script:\n";

    private const string PromptEditScript = "The script is already partially written and needs to be modified " +
                                            "or completed. Do not change the class name.\n" +
                                            "The current code that needs to be replaced is the following:\n";

    private const string CorePromptPrefix = "The task is described as follows:\n";

    public static string Compose(string input, [CanBeNull] MonoScript targetScript) {
        var enabledGuidingPrompts = Settings.instance.GetEnabledGuidingPrompts();
        var guidingPromptsString = enabledGuidingPrompts.Aggregate("", (current, guide) => current + $" - {guide}\n");
        var prompt = $"{PromptStart}{GuidingPromptsPrefix}{guidingPromptsString}";

        if (targetScript != null) {
            var scriptText = ScriptAsset.Load(targetScript);
            prompt += $"{PromptEditScript}{scriptText}\n";
        }

        prompt += $"{CorePromptPrefix}" + input;
        return prompt;
    }
}
}