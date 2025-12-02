using System;
#if !WPF
using Microsoft.UI.Xaml.Controls;

namespace PilotAIAssistantControl {
	/// <summary>
	/// Extension methods for WinUI TextBox to provide WPF-compatible functionality.
	/// </summary>
	internal static class WinUITextBoxExtensions {
		/// <summary>
		/// Gets the zero-based line index from the given character index.
		/// This mimics WPF's TextBox.GetLineIndexFromCharacterIndex() method.
		/// </summary>
		/// <param name="textBox">The TextBox instance</param>
		/// <param name="charIndex">The zero-based character index</param>
		/// <returns>The zero-based line index</returns>
		public static int GetLineIndexFromCharacterIndex(this TextBox textBox, int charIndex) {
			if (string.IsNullOrEmpty(textBox.Text) || charIndex <= 0)
				return 0;

			// Get text up to the character index
			var text = textBox.Text.Substring(0, Math.Min(charIndex, textBox.Text.Length));

			// Count newlines to determine line index
			int lineCount = 0;
			for (int i = 0; i < text.Length; i++) {
				if (text[i] == '\n')
					lineCount++;
			}

			return lineCount;
		}
	}
}
#endif
