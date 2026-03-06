using System;


#if WPF
using System.Windows;
using System.Windows.Controls;
using MdXaml;
#else
using CommunityToolkit.WinUI.UI.Controls;
//using CommunityToolkit.WinUI.Controls;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
#endif

namespace PilotAIAssistantControl {
	/// <summary>
	/// Cross-platform markdown viewer that wraps platform-specific markdown controls.
	/// WPF: Uses MdXaml.MarkdownScrollViewer
	/// WinUI: Uses UserControl with TextBlock (basic fallback - can be enhanced with proper markdown library later)
	/// </summary>
#if WPF
	public class MarkdownViewer : MarkdownScrollViewer {

		public MarkdownViewer() {
			// Configure MdXaml defaults
			VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
			Background = System.Windows.Media.Brushes.Transparent;
			MarkdownStyleName = "Sasabune";
		}
	}
#else
	public class MarkdownViewer : UserControl {
		private readonly MarkdownTextBlock _markdownBlock;

		// --- Dependency Properties ---

		public static readonly DependencyProperty MarkdownProperty =
			DependencyProperty.Register(
				nameof(Markdown),
				typeof(string),
				typeof(MarkdownViewer),
				new PropertyMetadata(string.Empty, OnMarkdownChanged));

		public string Markdown {
			get => (string)GetValue(MarkdownProperty);
			set => SetValue(MarkdownProperty, value);
		}

		public static readonly DependencyProperty CodeBackgroundBrushProperty =
			DependencyProperty.Register(
				nameof(CodeBackgroundBrush),
				typeof(Brush),
				typeof(MarkdownViewer),
				new PropertyMetadata(null, OnCodeBrushChanged));

		public Brush? CodeBackgroundBrush {
			get => (Brush?)GetValue(CodeBackgroundBrushProperty);
			set => SetValue(CodeBackgroundBrushProperty, value);
		}

		public static readonly DependencyProperty CodeForegroundBrushProperty =
			DependencyProperty.Register(
				nameof(CodeForegroundBrush),
				typeof(Brush),
				typeof(MarkdownViewer),
				new PropertyMetadata(null, OnCodeBrushChanged));

		public Brush? CodeForegroundBrush {
			get => (Brush?)GetValue(CodeForegroundBrushProperty);
			set => SetValue(CodeForegroundBrushProperty, value);
		}

		public static readonly DependencyProperty CodeBorderBrushProperty =
			DependencyProperty.Register(
				nameof(CodeBorderBrush),
				typeof(Brush),
				typeof(MarkdownViewer),
				new PropertyMetadata(null, OnCodeBrushChanged));

		public Brush? CodeBorderBrush {
			get => (Brush?)GetValue(CodeBorderBrushProperty);
			set => SetValue(CodeBorderBrushProperty, value);
		}

		// --- Callbacks ---

		private static void OnMarkdownChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
			var viewer = (MarkdownViewer)d;
			viewer._markdownBlock.Text = e.NewValue as string ?? string.Empty;
		}

		private static void OnCodeBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
			var viewer = (MarkdownViewer)d;
			viewer.ApplyCodeBrushes();
		}

		// --- Constructor ---

		public MarkdownViewer() {
			_markdownBlock = new MarkdownTextBlock {
				UseSyntaxHighlighting = false,
				IsTextSelectionEnabled = true,
				Background = new SolidColorBrush(Colors.Transparent),
				Margin = new Thickness(0),
				Padding = new Thickness(0),
				// Static styling (non-theme-dependent)
				CodeBorderThickness = new Thickness(1),
				CodePadding = new Thickness(8),
				CodeMargin = new Thickness(0, 6, 0, 6),
				InlineCodeBorderThickness = new Thickness(1),
				InlineCodePadding = new Thickness(4, 2, 4, 2)
			};

			Content = _markdownBlock;
		}

		private void ApplyCodeBrushes() {
			if (CodeBackgroundBrush != null) {
				_markdownBlock.CodeBackground = CodeBackgroundBrush;
				_markdownBlock.InlineCodeBackground = CodeBackgroundBrush;
			}
			if (CodeForegroundBrush != null) {
				_markdownBlock.CodeForeground = CodeForegroundBrush;
				_markdownBlock.InlineCodeForeground = CodeForegroundBrush;
			}
			if (CodeBorderBrush != null) {
				_markdownBlock.CodeBorderBrush = CodeBorderBrush;
				_markdownBlock.InlineCodeBorderBrush = CodeBorderBrush;
			}
		}
	}
#endif
}
