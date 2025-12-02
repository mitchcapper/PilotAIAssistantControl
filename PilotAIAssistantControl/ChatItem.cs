using System.Collections.Generic;
using System.Text.RegularExpressions;

#if WPF
using System.Windows;
using System.Windows.Media;
#else
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
#endif

namespace PilotAIAssistantControl {

	public class ChatItem {
		public string Message { get; set; } = string.Empty;
		public string Sender { get; set; } = string.Empty;

		// Default properties using the helper
		public Brush BackgroundColor { get; set; } = GetBrush(Colors.White);
		public Brush SenderColor { get; set; } = GetBrush(Colors.Gray);

		public HorizontalAlignment Alignment { get; set; } = HorizontalAlignment.Left;
		public List<CodeBlock> CodeBlocks { get; set; } = new();
		public bool HasCodeBlocks => CodeBlocks.Count > 0;

		// --- Factory Methods ---

		public static ChatItem CreateUserMessage(string message) {
			return new ChatItem {
				Message = message,
				Sender = "You",
				// AliceBlue is a standard "Light Blue"
				BackgroundColor = GetBrush(Colors.AliceBlue),
				// DarkSlateBlue is readable against light backgrounds
				SenderColor = GetBrush(Colors.DarkSlateBlue),
				Alignment = HorizontalAlignment.Right
			};
		}

		private static Regex FindCodeBlockEnd = new(@"^```$", RegexOptions.Multiline);

		public static ChatItem CreateAiMessage(string message) {
			// bit hacky to make sure scrollbar doesn't make readability hard
			message = FindCodeBlockEnd.Replace(message, "\n```");

			return new ChatItem {
				Message = message,
				Sender = "AI Assistant",
				// WhiteSmoke is a standard "Light Gray" perfect for message bubbles
				BackgroundColor = GetBrush(Colors.WhiteSmoke),
				SenderColor = GetBrush(Colors.SeaGreen),
				Alignment = HorizontalAlignment.Left,
				CodeBlocks = CodeBlock.ExtractCodeBlocks(message)
			};
		}

		public static ChatItem CreateSystemMessage(string message, bool isError = false) {
			// Setup colors based on error state using Named Colors
			Color bgColor = isError ? Colors.MistyRose : Colors.Cornsilk;
			Color txtColor = isError ? Colors.DarkRed : Colors.DarkOrange;

			return new ChatItem {
				Message = message,
				Sender = "System",
				BackgroundColor = GetBrush(bgColor),
				SenderColor = GetBrush(txtColor),
				Alignment = HorizontalAlignment.Stretch
			};
		}

		// --- Helpers ---

		/// <summary>
		/// creates a frozen SolidColorBrush for WPF or a standard one for WinUI
		/// </summary>
		private static Brush GetBrush(Color color) {
#if WPF
            var brush = new SolidColorBrush(color);
            // Freezing is important in WPF for performance and thread safety 
            // (similar to how Brushes.White works)
            brush.Freeze(); 
            return brush;
#else
			return new SolidColorBrush(color);
#endif
		}
	}
}
