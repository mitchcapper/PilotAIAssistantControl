using System;


#if WPF
using System.Windows;
using System.Windows.Controls;
using MdXaml;
#else
using CommunityToolkit.WinUI.UI.Controls;
//using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
#endif

namespace PilotAIAssistantControl {
	/// <summary>
	/// Cross-platform markdown viewer that wraps platform-specific markdown controls.
	/// WPF: Uses MdXaml.MarkdownScrollViewer
	/// WinUI: Uses UserControl with TextBlock (basic fallback - can be enhanced with proper markdown library later)
	/// </summary>
#if WPF
	public class MarkdownViewer : MarkdownScrollViewer {
		public static readonly DependencyProperty MarkdownProperty =
			DependencyProperty.Register(
				nameof(Markdown),
				typeof(string),
				typeof(MarkdownViewer),
				new PropertyMetadata(string.Empty));

		public string Markdown {
			get => (string)GetValue(MarkdownProperty);
			set => SetValue(MarkdownProperty, value);
		}

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

		private static void OnMarkdownChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
			var viewer = (MarkdownViewer)d;
			viewer._markdownBlock.Text = e.NewValue as string ?? string.Empty;
		}

		public MarkdownViewer() {
			//_markdownBlock = new MarkdownTextBlock(){UseAutoLinks=true,UseEmphasisExtras=true,UseListExtras=true,UseTaskLists=true,UsePipeTables=true, };
			_markdownBlock = new MarkdownTextBlock(){UseSyntaxHighlighting=true};
			Content = _markdownBlock;
		}
	}
#endif
}
