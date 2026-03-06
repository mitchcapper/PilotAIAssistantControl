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
		private const string UserSender = "You";
		private const string AiSender = "AI Assistant";
		private const string SystemSender = "System";

#if !WPF
		// Application.Current.RequestedTheme is set once at startup and does NOT update
		// when the system theme changes at runtime. The UI layer sets this from ActualTheme
		// so SearchThemeDictionaries resolves the correct theme dictionary.
		//internal static bool IsDarkTheme { get; set { } } = true;
		internal static bool IsDarkTheme { get; set; }
#endif

		public string Message { get; set; } = string.Empty;
		public string Sender { get; set; } = string.Empty;
		public bool IsSystemError { get; set; }

		// Default properties use theme-aware brushes (with safe fallbacks)
		public Brush BackgroundColor { get; set; } = GetThemeBrush("CardBackgroundFillColorDefaultBrush", Colors.White);
		public Brush SenderColor { get; set; } = GetThemeBrush("TextFillColorSecondaryBrush", Colors.Gray);

		public HorizontalAlignment Alignment { get; set; } = HorizontalAlignment.Left;
		public List<CodeBlock> CodeBlocks { get; set; } = new();
		public bool HasCodeBlocks => CodeBlocks.Count > 0;
		public bool IsAI => Sender == AiSender;
		public bool IsUser => Sender == UserSender;

		// --- Factory Methods ---

		public static ChatItem CreateUserMessage(string message) {
			return ApplyThemeBrushes(new ChatItem {
				Message = message,
				Sender = UserSender,
				Alignment = HorizontalAlignment.Right
			});
		}

		private static Regex FindCodeBlockEnd = new(@"^```$", RegexOptions.Multiline);

		public static ChatItem CreateAiMessage(string message) {
			// bit hacky to make sure scrollbar doesn't make readability hard
			message = FindCodeBlockEnd.Replace(message, "\n```");

			return ApplyThemeBrushes(new ChatItem {
				Message = message,
				Sender = AiSender,
				Alignment = HorizontalAlignment.Left,
				CodeBlocks = CodeBlock.ExtractCodeBlocks(message)
			});
		}

		public static ChatItem CreateSystemMessage(string message, bool isError = false) {
			return ApplyThemeBrushes(new ChatItem {
				Message = message,
				Sender = SystemSender,
				IsSystemError = isError,
				Alignment = HorizontalAlignment.Stretch
			});
		}

		public ChatItem CreateRethemedCopy() {
			return ApplyThemeBrushes(new ChatItem {
				Message = Message,
				Sender = Sender,
				IsSystemError = IsSystemError,
				Alignment = Alignment,
				CodeBlocks = CodeBlocks
			});
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

		internal static Brush GetThemeBrush(string key, Color fallbackColor) {
#if WPF
			if (Application.Current?.TryFindResource(key) is Brush resourceBrush) {
				return resourceBrush;
			}
#else
			// TryGetValue doesn't search ThemeDictionaries inside MergedDictionaries,
			// so search explicitly based on the current app theme.
			var resources = Application.Current?.Resources;
			if (resources != null) {
				string theme = IsDarkTheme ? "Dark" : "Light";
				if (SearchThemeDictionaries(resources, key, theme) is Brush found)
					return found;
				if (resources.TryGetValue(key, out object? resource) && resource is Brush resourceBrush)
					return resourceBrush;
			}
#endif
			return GetBrush(fallbackColor);
		}

#if !WPF
		private static Brush? SearchThemeDictionaries(ResourceDictionary dict, string key, string theme) {
			if (dict.ThemeDictionaries.Count > 0 &&
				dict.ThemeDictionaries.TryGetValue(theme, out object? td) &&
				td is ResourceDictionary themed &&
				themed.TryGetValue(key, out object? resource) &&
				resource is Brush brush) {
				return brush;
			}
			foreach (var merged in dict.MergedDictionaries) {
				if (SearchThemeDictionaries(merged, key, theme) is Brush found)
					return found;
			}
			return null;
		}
#endif

		private static ChatItem ApplyThemeBrushes(ChatItem item) {
			if (item.Sender == UserSender) {
				item.BackgroundColor = GetThemeBrush("TextFillColorInverseBrush", Colors.Orange);
				item.SenderColor = GetThemeBrush("TextOnAccentFillColorSecondary", Colors.Gray);
				return item;
			}

			if (item.Sender == AiSender) {
				item.BackgroundColor = GetThemeBrush("CardBackgroundFillColorSecondaryBrush", Colors.WhiteSmoke);
				item.SenderColor = GetThemeBrush("TextFillColorSecondaryBrush", Colors.SeaGreen);
				return item;
			}

			if (item.Sender == SystemSender) {
				string bgKey = item.IsSystemError ? "InfoBarErrorSeverityBackgroundBrush" : "InfoBarWarningSeverityBackgroundBrush";
				item.BackgroundColor = GetThemeBrush(bgKey, item.IsSystemError ? Colors.MistyRose : Colors.Cornsilk);
				item.SenderColor = GetThemeBrush("TextFillColorPrimaryBrush", item.IsSystemError ? Colors.DarkRed : Colors.DarkOrange);
				return item;
			}

			item.BackgroundColor = GetThemeBrush("CardBackgroundFillColorDefaultBrush", Colors.White);
			item.SenderColor = GetThemeBrush("TextFillColorSecondaryBrush", Colors.Gray);
			return item;
		}
	}
}
