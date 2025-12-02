using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace PilotAIAssistantControl {
	public interface ICodeBlock {
		string Language { get; }
		string Code { get; }
	}

	public record BlockAction(ICodeblockAction Action, CodeBlock Block);
	public class GenericCodeblockAction : ICodeblockAction {
		public GenericCodeblockAction(string displayName, Func<ICodeBlock, Task<bool>> DoActionDel) {
			DisplayName = displayName;
			this.DoActionDel = DoActionDel;
		}
		public string DisplayName { get; }
		public string Tooltip { get; set; }
		public string FeedbackOnAction { get; set; } = "✓ Done!";
		public Func<ICodeBlock, bool> IsVisibleDel { get; set; } = (block) => true;
		public Func<ICodeBlock, Task<bool>> DoActionDel { get; }

		public Task<bool> DoAction(ICodeBlock block) => DoActionDel(block);
		public static GenericCodeblockAction ClipboardAction = new GenericCodeblockAction("📋 Copy", async (block) => {
			for (var x = 0; x < 2; x++) {
				try {
					Clipboard.SetText(block.Code);
					return true;
				} catch {
					await Task.Delay(150);
				}
			}
			return false;
		}

		) {
			Tooltip = "📋 Copy to Clipboard",
			FeedbackOnAction = "✓ Copied!"
		};

	}
	public interface ICodeblockAction {
		Task<bool> DoAction(ICodeBlock block);
		/// <summary>
		/// ie 📋 Copy
		/// </summary>
		String DisplayName { get; }

		String Tooltip => DisplayName;
		String FeedbackOnAction => "✓ Done!";

		bool IsVisible(ICodeBlock block) => true;

	}

	// Represents an extracted code block from markdown
	public class CodeBlock : ICodeBlock {
		/// <summary>
		/// Extracts code blocks from markdown content.
		/// </summary>
		public static List<CodeBlock> ExtractCodeBlocks(string markdown) {
			var blocks = new List<CodeBlock>();
			var regex = new Regex(
				@"```(\w*)\s*\n([\s\S]*?)```",
				RegexOptions.Multiline);

			int index = 0;
			foreach (Match match in regex.Matches(markdown)) {
				blocks.Add(new CodeBlock {
					Language = match.Groups[1].Value,
					Code = match.Groups[2].Value.Trim(),
					Index = index++
				});
			}
			return blocks;
		}
		public string Language { get; set; } = string.Empty;
		public string Code { get; set; } = string.Empty;
		public int Index { get; set; }
		public BlockAction[] Actions { get; set; }
	}





}
