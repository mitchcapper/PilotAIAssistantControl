using System;
using System.Globalization;

#if WPF
using System.Windows;
using System.Windows.Data;
#else
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
#endif

namespace PilotAIAssistantControl {

#if !WPF
	/// <summary>
	/// Converts a ChatItem to the appropriate Border Style for chat bubbles.
	/// Each Style uses {ThemeResource} for backgrounds, so they auto-update on theme changes.
	/// </summary>
	public class ChatItemToBubbleStyleConverter : IValueConverter {
		public Style? UserStyle { get; set; }
		public Style? AiStyle { get; set; }
		public Style? SystemWarningStyle { get; set; }
		public Style? SystemErrorStyle { get; set; }

		public object? Convert(object value, Type targetType, object parameter, string language) {
			if (value is ChatItem item) {
				if (item.IsUser) return UserStyle;
				if (item.IsAI) return AiStyle;
				if (item.IsSystemError) return SystemErrorStyle;
				return SystemWarningStyle;
			}
			return AiStyle;
		}

		public object ConvertBack(object value, Type targetType, object parameter, string language)
			=> throw new NotImplementedException();
	}
#endif
}
