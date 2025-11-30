using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace PilotAIAssistantControl {
	/// <summary>
	/// A WPF UserControl that wraps UCAI with an Expander control and provides
	/// automatic grid column/row resizing on expand/collapse.
	/// Bind TargetColumn (for horizontal) or TargetRow (for vertical) to enable grid integration.
	/// </summary>
	public partial class UCAIExpandable : UserControl {

		public void Configure(AIOptions options) => InternalAiControl.Configure(options);
		public AIUserConfig ExportData() => InternalAiControl.ExportData();
		public void ImportData(AIUserConfig? data) => InternalAiControl.ImportData(data);

		#region Dependency Properties

		public static readonly DependencyProperty TargetColumnProperty =
			DependencyProperty.Register(
				nameof(TargetColumn),
				typeof(ColumnDefinition),
				typeof(UCAIExpandable),
				new PropertyMetadata(null, OnTargetColumnChanged));

		public ColumnDefinition TargetColumn {
			get => (ColumnDefinition)GetValue(TargetColumnProperty);
			set => SetValue(TargetColumnProperty, value);
		}

		private static void OnTargetColumnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
			var control = (UCAIExpandable)d;
			control.ValidateOrientation();
			control.ApplyCurrentState();
		}

		public static readonly DependencyProperty TargetRowProperty =
			DependencyProperty.Register(
				nameof(TargetRow),
				typeof(RowDefinition),
				typeof(UCAIExpandable),
				new PropertyMetadata(null, OnTargetRowChanged));

		public RowDefinition TargetRow {
			get => (RowDefinition)GetValue(TargetRowProperty);
			set => SetValue(TargetRowProperty, value);
		}

		private static void OnTargetRowChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
			var control = (UCAIExpandable)d;
			control.ValidateOrientation();
			control.ApplyCurrentState();
		}

		public static readonly DependencyProperty DefaultSizeProperty =
			DependencyProperty.Register(
				nameof(DefaultSize),
				typeof(double),
				typeof(UCAIExpandable),
				new PropertyMetadata(400.0, OnSizePropertyChanged));

		public double DefaultSize {
			get => (double)GetValue(DefaultSizeProperty);
			set => SetValue(DefaultSizeProperty, value);
		}

		public static readonly DependencyProperty MinExpandedSizeProperty =
			DependencyProperty.Register(
				nameof(MinExpandedSize),
				typeof(double),
				typeof(UCAIExpandable),
				new PropertyMetadata(200.0, OnSizePropertyChanged));

		public double MinExpandedSize {
			get => (double)GetValue(MinExpandedSizeProperty);
			set => SetValue(MinExpandedSizeProperty, value);
		}

		public static readonly DependencyProperty CollapsedSizeProperty =
			DependencyProperty.Register(
				nameof(CollapsedSize),
				typeof(double),
				typeof(UCAIExpandable),
				new PropertyMetadata(40.0, OnSizePropertyChanged));

		public double CollapsedSize {
			get => (double)GetValue(CollapsedSizeProperty);
			set => SetValue(CollapsedSizeProperty, value);
		}

		private static void OnSizePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
			var control = (UCAIExpandable)d;
			control.ApplyCurrentState();
		}

		public static readonly DependencyProperty HeaderProperty =
			DependencyProperty.Register(
				nameof(Header),
				typeof(object),
				typeof(UCAIExpandable),
				new PropertyMetadata(null));

		public object Header {
			get => GetValue(HeaderProperty);
			set => SetValue(HeaderProperty, value);
		}

		public static readonly DependencyProperty IsExpandedProperty =
			DependencyProperty.Register(
				nameof(IsExpanded),
				typeof(bool),
				typeof(UCAIExpandable),
				new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

		public bool IsExpanded {
			get => (bool)GetValue(IsExpandedProperty);
			set => SetValue(IsExpandedProperty, value);
		}

		public static readonly DependencyProperty ExpandDirectionProperty =
			DependencyProperty.Register(
				nameof(ExpandDirection),
				typeof(ExpandDirection),
				typeof(UCAIExpandable),
				new PropertyMetadata(ExpandDirection.Right));

		public ExpandDirection ExpandDirection {
			get => (ExpandDirection)GetValue(ExpandDirectionProperty);
			set => SetValue(ExpandDirectionProperty, value);
		}

		private static object CreateDefaultHeader() {
			var textBlock = new TextBlock {
				Margin = new Thickness(5),
				FontSize = 15,
				VerticalAlignment = VerticalAlignment.Stretch,
				TextAlignment = TextAlignment.Center
			};
			textBlock.Inlines.Add("💬");
			textBlock.Inlines.Add(new LineBreak());
			textBlock.Inlines.Add("AI");
			return textBlock;
		}

		#endregion

		#region Public Properties

		public Expander ExpanderControl => InternalExpander;

		public UCAI AiControl => InternalAiControl;

		#endregion

		#region Private Fields

		private double _lastSize = 400.0;
		private bool _isInitialized = false;

		#endregion

		#region Constructor

		public UCAIExpandable() {
			InitializeComponent();
			Header = CreateDefaultHeader();
			Loaded += OnLoaded;
		}

		private void OnLoaded(object sender, RoutedEventArgs e) {
			_isInitialized = true;
			ApplyCurrentState();
		}

		#endregion

		#region Expand/Collapse Event Handlers

		private void InternalExpander_Expanded(object sender, RoutedEventArgs e) {
			ApplyExpandedState();
			InternalAiControl.TryAutoConnectOnExpand();
		}

		private void InternalExpander_Collapsed(object sender, RoutedEventArgs e) {
			ApplyCollapsedState();
		}

		#endregion

		#region Core Logic

		private void ValidateOrientation() {
			if (TargetColumn != null && TargetRow != null)
				throw new ArgumentException(
					"Cannot set both TargetColumn and TargetRow simultaneously. " +
					"UCAIExpandable must be either horizontal (TargetColumn) or vertical (TargetRow) orientation.");
		}

		private void ApplyCurrentState() {
			if (!_isInitialized) return;

			if (InternalExpander.IsExpanded)
				ApplyExpandedState();
			else
				ApplyCollapsedState();
		}

		private void ApplyExpandedState() {
			if (TargetColumn != null) {
				TargetColumn.MinWidth = MinExpandedSize;

				if (_lastSize > MinExpandedSize)
					TargetColumn.Width = new GridLength(_lastSize);
				else
					TargetColumn.Width = new GridLength(DefaultSize);
			} else if (TargetRow != null) {
				TargetRow.MinHeight = MinExpandedSize;

				if (_lastSize > MinExpandedSize)
					TargetRow.Height = new GridLength(_lastSize);
				else
					TargetRow.Height = new GridLength(DefaultSize);
			}
		}

		private void ApplyCollapsedState() {
			if (TargetColumn != null) {
				_lastSize = TargetColumn.ActualWidth;
				TargetColumn.MinWidth = CollapsedSize;
				TargetColumn.Width = new GridLength(CollapsedSize);
			} else if (TargetRow != null) {
				_lastSize = TargetRow.ActualHeight;
				TargetRow.MinHeight = CollapsedSize;
				TargetRow.Height = new GridLength(CollapsedSize);
			}
		}

		#endregion
	}
}
