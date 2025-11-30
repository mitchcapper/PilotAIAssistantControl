using PilotAIAssistantControl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestWPFApp {
	public interface OurEngineOptions {
		string Name { get; }
		string? ExportOptions(); // (JSON)
		string AIPatternType => "Regex";
		string AIPatternCodeblockType => "regex";
		string AIAdditionalSystemPrompt => "If the language supports named capture groups, use these by default. " +
				   "If the user has ignoring patterned whitespace enabled in the options, use multi-lines and minimal in-regex comments for complex regexes with nice whitespace formatting to make it more readable. ";
	}

	public class OurRegexEngine : OurEngineOptions {
		public string Name => "C#";

		public string? ExportOptions() {
			return "{SingleLine:On}";
		}
		public static OurRegexEngine Instance = new OurRegexEngine();
	}

	public class OurOptions : AIOptions {
		public OurEngineOptions Engine => OurRegexEngine.Instance;

		public override string ReferenceTextHeader => "Users current target text";
		public override string GetSystemPrompt() => $"You are a {Engine.Name} {Engine.AIPatternType} expert assistant. The user has questions about their {Engine.AIPatternType} patterns and target text. " +
				   $"Provide {Engine.AIPatternType} patterns inside Markdown code blocks (```{Engine.AIPatternCodeblockType} ... ```). " +
				   "Explain how the pattern works briefly. " +
					Engine.AIAdditionalSystemPrompt +
				   $"They currently have these engine options enabled:\n```json\n{Engine.ExportOptions()}\n```";

		public override string HintForUserInput => "Ask about a pattern or matching...";
		public override string ReferenceTextDisplayName => "Target Text";
		public override string FormatUserQuestion(string userQuestion) => $"Current pattern:\n```regex\n{MainWindow.Instance.txtRegex.Text}\n```\n\nMy question: {userQuestion}";
		 
		public override string GetCurrentReferenceText() => MainWindow.Instance.txtTest.Text;
		public override IEnumerable<CodeblockAction> CodeblockActions => [ GenericCodeblockAction.ClipboardAction,
				new GenericCodeblockAction("📝 Use as Pattern", async ( block ) =>
				{

					MainWindow.Instance.txtRegex.Text = block.Code;
					return true;
				} )
				{
					Tooltip="Use this code block as the regex pattern",
					FeedbackOnAction="✓ Applied!"
				}
		];
		public override void HandleDebugMessage(string msg) => System.Diagnostics.Debug.WriteLine(msg);
	}
}
