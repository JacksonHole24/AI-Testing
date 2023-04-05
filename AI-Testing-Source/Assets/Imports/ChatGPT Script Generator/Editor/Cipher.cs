using System;
using System.Text;

namespace ScriptGenerator {
public abstract class Cipher {
    public static string Hide(string input, string key) {
        return Process(input, key, true);
    }

    public static string Reveal(string input, string key) {
        return Process(input, key, false);
    }

    private static string Process(string input, string key, bool forward) {
        if (key.Length == 0) {
            throw new ArgumentException("Key must not be empty.");
        }

        StringBuilder output = new StringBuilder(input.Length);
        int keyIndex = 0;

        foreach (char inputChar in input) {
            int keyChar = key[keyIndex] % 32;
            int shiftAmount = forward ? keyChar : -keyChar;
            char shiftedChar = (char)(inputChar + shiftAmount);
            output.Append(shiftedChar);

            keyIndex = (keyIndex + 1) % key.Length;
        }

        return output.ToString();
    }
}
}