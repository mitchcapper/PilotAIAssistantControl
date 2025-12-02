using System;
using System.Globalization;
using System.Linq;
#if WPF
using System.Windows;
using System.Windows.Data;
#else
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
#endif

namespace PilotAIAssistantControl {

	public class BoolNullVisibilityCollapsedConverter() : BaseBoolNullConverter(Visibility.Visible, Visibility.Collapsed), IValueConverter {
	}

	/// <summary>
	/// Converter for showing placeholder text when TextBox is empty.
	/// Returns Visible when string is empty/null, Collapsed when string has content.
	/// </summary>
	public class EmptyStringToVisibilityConverter : IValueConverter {
		public object Convert(object value, Type targetType, object parameter, CultureInfo? culture) {
			var text = value as string;
			return string.IsNullOrEmpty(text) ? Visibility.Visible : Visibility.Collapsed;
		}

		// WinUI overload
		public object Convert(object value, Type targetType, object parameter, string language)
			=> Convert(value, targetType, parameter, default(CultureInfo));

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
			=> throw new NotImplementedException();

		public object ConvertBack(object value, Type targetType, object parameter, string language)
			=> throw new NotImplementedException();
	}
	public abstract class BaseBoolNullConverter(object true_val, object false_val) {

		public object Convert(object value, Type targetType, object parameter, CultureInfo? culture) {
			if (culture?.IetfLanguageTag == "de-DE"){
				System.Diagnostics.Debug.WriteLine("BaseBoolNullConverter: debugger tag found");
			}
			var res = GetConvertResult(value, parameter);
			if (targetType == typeof(Boolean))
				return res;
			return res ? true_val : false_val;
		}
		//uwp
		public object Convert(object value, Type targetType, object parameter, string language) => Convert(value, targetType, parameter, default(CultureInfo));
		public static bool GetConvertResult(object value, object parameter) {
			bool res = true;
			if (value == null)
				res = false;
			else if (value is string sv)
				res = !String.IsNullOrWhiteSpace(sv);
			else if (value is bool bv)
				res = bv;
			if (parameter != null && ((parameter.GetType() == typeof(bool) && ((bool)parameter)) || (parameter.GetType() == typeof(string) && new string[] { "true", "1" }.Contains(parameter as string))))
				res = !res;
			return res;
		}
		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
		public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
	}
}
