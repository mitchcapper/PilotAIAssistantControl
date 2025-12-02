
using PilotAIAssistantControl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#if !WPF
using TestWinUIApp;
using Microsoft.UI.Xaml.Controls;
#else
using System.Windows.Controls;
#endif
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
	public class SimpleExplainerOptions : AIOptions {
		public override string GetSystemPrompt()  => "You are an expert in regular expressions and the user needs you to explain how a given regex pattern works. You can assume they know programming but are not a regular expression expert.  By default provide a clear but concise explanation for how the user's regular expression works. For simple regular expressions this should only be a few sentences, for complex regex maybe a paragraph or a bit more.  Your goal is to summarize it initially only. Then let the user know they can ask followup questions for examples of it matching, a more through breakdown, etc.   Do not give examples by default.  Use markdown formatting for it to be as easy to read as possible.";

		public override string HintForUserInput => "Provide a regex pattern to explain...";
		public override string FormatUserQuestion(string userQuestion) => $"Please explain the following regex pattern:\n```regex\n{userQuestion}\n```";
		public override REFERENCE_TEXT_REPLACE_ACTION ReplaceAction => REFERENCE_TEXT_REPLACE_ACTION.ReferenceTextDisabled;
		public override void HandleDebugMessage(string msg) => System.Diagnostics.Debug.WriteLine(msg);

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
		internal TextBox RegexBox =>
#if WPF
			MainWindow.Instance.txtRegex;
#else
			MainWindow.Instance.txtRegexPublic;
			#endif
		public override string FormatUserQuestion(string userQuestion) => $"Current pattern:\n```regex\n{RegexBox.Text}\n```\n\nMy question: {userQuestion}";
		 
		public override string GetCurrentReferenceText() =>
#if WPF
			MainWindow.Instance.txtTest.Text;
			#else
			MainWindow.Instance.txtTestPublic.Text;
			#endif
		public override IEnumerable<ICodeblockAction> CodeblockActions => [ GenericCodeblockAction.ClipboardAction,
				new GenericCodeblockAction("📝 Use as Pattern", async ( block ) =>
				{

					RegexBox.Text = block.Code;
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
