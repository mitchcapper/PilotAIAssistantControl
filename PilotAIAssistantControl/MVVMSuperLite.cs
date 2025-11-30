using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace PilotAIAssistantControl.MVVM {
	public class BaseNotifyObject : INotifyPropertyChanged {
		public event PropertyChangedEventHandler? PropertyChanged;
		protected bool Set<T>(ref T field, in T newValue, [CallerMemberName] string? propertyName = null) {
			if (propertyName == null)
				return false;
			if (EqualityComparer<T>.Default.Equals(field, newValue))
				return false;

			field = newValue;
			RaisePropertyChanged(propertyName);
			return true;
		}
		protected bool Set<T>(Expression<Func<T>> propertyExpression, ref T field, in T newValue) => Set<T>(ref field, newValue, GetPropertyName(propertyExpression));
		/// <summary>
		/// Notify additional properties if the value is true, can be used like:
		/// set => ChangedIf(Set(ref _proxy_ip, value),()=>ProxyAddy,()=>proxy_port);
		/// </summary>
		/// <param name="val"></param>
		/// <param name="propertyExpressions"></param>
		/// <returns></returns>
		protected bool ChangedIf(bool val, params LambdaExpression[] propertyExpressions) {
			if (val && PropertyChanged != null) {
				foreach (var prop in propertyExpressions) {
					var name = GetPropertyName(prop);
					if (!String.IsNullOrWhiteSpace(name))
						RaisePropertyChanged(name);
				}
			}
			return val;
		}
		public virtual void RaisePropertyChanged<T>(Expression<Func<T>> propertyExpression) => RaisePropertyChanged(GetPropertyName(propertyExpression));
		public virtual void RaisePropertyChanged([CallerMemberName] string propertyName = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		protected static string GetPropertyName(LambdaExpression propertyExpression) => GetPropertyInfo(propertyExpression).Name;
		protected static PropertyInfo GetPropertyInfo(LambdaExpression propertyExpression) {
			if (propertyExpression == null)
				throw new ArgumentNullException("propertyExpression");
			var body = propertyExpression.Body as MemberExpression;

			if (body == null)
				throw new ArgumentException("Invalid argument", "propertyExpression");

			var property = body.Member as PropertyInfo;
			if (property == null)
				throw new ArgumentException("Argument is not a property", "propertyExpression");
			return property;
		}
	}
}
