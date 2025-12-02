using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PilotAIAssistantControl {

	/// <summary>
	/// Expand direction for FauxExpander - includes horizontal directions that WinUI's native Expander doesn't support.
	/// </summary>
	public enum FauxExpandDirection {
		Down,
		Up,
		Left,
		Right
	}

	/// <summary>
	/// A faux expander control for WinUI3 that emulates horizontal expansion using a ToggleButton.
	/// WinUI3's native Expander only supports vertical (Up/Down) expansion, so this provides
	/// Left/Right expansion support needed for side panel layouts.
	/// </summary>
	public sealed partial class FauxExpander : UserControl {

		public FauxExpander() {
			InitializeComponent();
			Loaded += FauxExpander_Loaded;
		}

		private void FauxExpander_Loaded(object sender, RoutedEventArgs e) {
			ApplyLayout();
		}

		#region Dependency Properties

		public static readonly DependencyProperty IsExpandedProperty =
			DependencyProperty.Register(
				nameof(IsExpanded),
				typeof(bool),
				typeof(FauxExpander),
				new PropertyMetadata(false, OnIsExpandedChanged));

		public bool IsExpanded {
			get => (bool)GetValue(IsExpandedProperty);
			set => SetValue(IsExpandedProperty, value);
		}

		private static void OnIsExpandedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
			var control = (FauxExpander)d;
			if ((bool)e.NewValue)
				control.RaiseExpanding();
			else
				control.RaiseCollapsed();
		}

		public static readonly DependencyProperty HeaderProperty =
			DependencyProperty.Register(
				nameof(Header),
				typeof(object),
				typeof(FauxExpander),
				new PropertyMetadata(null));

		public object Header {
			get => GetValue(HeaderProperty);
			set => SetValue(HeaderProperty, value);
		}

		public static readonly DependencyProperty FauxContentProperty =
			DependencyProperty.Register(
				nameof(FauxContent),
				typeof(object),
				typeof(FauxExpander),
				new PropertyMetadata(null));

		public object FauxContent {
			get => GetValue(FauxContentProperty);
			set => SetValue(FauxContentProperty, value);
		}

		public static readonly DependencyProperty ExpandDirectionProperty =
			DependencyProperty.Register(
				nameof(ExpandDirection),
				typeof(FauxExpandDirection),
				typeof(FauxExpander),
				new PropertyMetadata(FauxExpandDirection.Down, OnExpandDirectionChanged));

		public FauxExpandDirection ExpandDirection {
			get => (FauxExpandDirection)GetValue(ExpandDirectionProperty);
			set => SetValue(ExpandDirectionProperty, value);
		}

		private static void OnExpandDirectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
			((FauxExpander)d).ApplyLayout();
		}

		#endregion

		#region Events

		public event EventHandler<FauxExpanderExpandingEventArgs>? Expanding;
		public event EventHandler<FauxExpanderCollapsedEventArgs>? Collapsed;

		private void RaiseExpanding() {
			Expanding?.Invoke(this, new FauxExpanderExpandingEventArgs());
		}

		private void RaiseCollapsed() {
			Collapsed?.Invoke(this, new FauxExpanderCollapsedEventArgs());
		}

		#endregion

		#region Layout

		private void ApplyLayout() {
			switch (ExpandDirection) {
				case FauxExpandDirection.Right:
					HeaderColumn.Width = GridLength.Auto;
					ContentColumn.Width = new GridLength(1, GridUnitType.Star);
					HeaderRow.Height = new GridLength(1, GridUnitType.Star);
					ContentRow.Height = new GridLength(1, GridUnitType.Star);
					Grid.SetColumn(ExpandToggle, 0);
					Grid.SetRow(ExpandToggle, 0);
					Grid.SetRowSpan(ExpandToggle, 2);
					Grid.SetColumnSpan(ExpandToggle, 1);
					Grid.SetColumn(ContentArea, 1);
					Grid.SetRow(ContentArea, 0);
					Grid.SetRowSpan(ContentArea, 2);
					Grid.SetColumnSpan(ContentArea, 1);
					break;
				case FauxExpandDirection.Left:
					HeaderColumn.Width = new GridLength(1, GridUnitType.Star);
					ContentColumn.Width = GridLength.Auto;
					HeaderRow.Height = new GridLength(1, GridUnitType.Star);
					ContentRow.Height = new GridLength(1, GridUnitType.Star);
					Grid.SetColumn(ExpandToggle, 1);
					Grid.SetRow(ExpandToggle, 0);
					Grid.SetRowSpan(ExpandToggle, 2);
					Grid.SetColumnSpan(ExpandToggle, 1);
					Grid.SetColumn(ContentArea, 0);
					Grid.SetRow(ContentArea, 0);
					Grid.SetRowSpan(ContentArea, 2);
					Grid.SetColumnSpan(ContentArea, 1);
					break;
				case FauxExpandDirection.Down:
					HeaderColumn.Width = new GridLength(1, GridUnitType.Star);
					ContentColumn.Width = new GridLength(1, GridUnitType.Star);
					HeaderRow.Height = GridLength.Auto;
					ContentRow.Height = new GridLength(1, GridUnitType.Star);
					Grid.SetColumn(ExpandToggle, 0);
					Grid.SetRow(ExpandToggle, 0);
					Grid.SetRowSpan(ExpandToggle, 1);
					Grid.SetColumnSpan(ExpandToggle, 2);
					Grid.SetColumn(ContentArea, 0);
					Grid.SetRow(ContentArea, 1);
					Grid.SetRowSpan(ContentArea, 1);
					Grid.SetColumnSpan(ContentArea, 2);
					break;
				case FauxExpandDirection.Up:
					HeaderColumn.Width = new GridLength(1, GridUnitType.Star);
					ContentColumn.Width = new GridLength(1, GridUnitType.Star);
					HeaderRow.Height = new GridLength(1, GridUnitType.Star);
					ContentRow.Height = GridLength.Auto;
					Grid.SetColumn(ExpandToggle, 0);
					Grid.SetRow(ExpandToggle, 1);
					Grid.SetRowSpan(ExpandToggle, 1);
					Grid.SetColumnSpan(ExpandToggle, 2);
					Grid.SetColumn(ContentArea, 0);
					Grid.SetRow(ContentArea, 0);
					Grid.SetRowSpan(ContentArea, 1);
					Grid.SetColumnSpan(ContentArea, 2);
					break;
			}
		}

		#endregion
	}

	public class FauxExpanderExpandingEventArgs : EventArgs { }
	public class FauxExpanderCollapsedEventArgs : EventArgs { }
}
