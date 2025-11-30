using System.Collections.Generic;

namespace PilotAIAssistantControl {

	public abstract class AIOptions {
		public enum REFERENCE_TEXT_REPLACE_ACTION {
			/// <summary>
			/// Disable reference text functionality, will not show in UI either
			/// </summary>
			ReferenceTextDisabled,
			/// <summary>
			/// This will change the old reference text to a placeholder indicating it has changed and the new version will appear later in the chat history, exact placeholder text can be controlled via PlaceholderTextForReferenceTextRemoval.
			/// </summary>
			ChangeOldToPlaceholder,
			/// <summary>
			/// This leaves the old reference text in place in the chat history even if it changes later. It may eat up excess tokens but hopefully shouldn't be too confusing to the AI as it can see the newer version later.  It also allows the AI to understand how the reference text has changed over time.
			/// </summary>
			LeaveOldInplace,
			/// <summary>
			/// UpdateInPlace means it appears in the chat history where it originally appeared, this might be confusing as the AI's answers were based on the old text and it may confuse the current AI as why the AI previously responded as it did.
			/// </summary>
			UpdateInPlace,
			/// <summary>
			/// This deletes the old reference text from the chat history entirely when it changes. This might cause confusion for the current AI as it won't know exactly what the previous AI was basing its responses off.
			/// </summary>
			DeleteOld,

		}
		public virtual IAIModelProvider[] Providers => IAIModelProvider.DefaultProviders;
		/// <summary>
		/// For large reference texts we don't want to keep sending them in the chat history if they change so this controls how we handle them.  Depending on the action may cause confusion for the AI or waste tokens, ChangeOldToPlaceholder is likely best.
		/// </summary>
		public virtual REFERENCE_TEXT_REPLACE_ACTION ReplaceAction => REFERENCE_TEXT_REPLACE_ACTION.ChangeOldToPlaceholder;

		public virtual bool IsReferenceTextEnabled => ReplaceAction != REFERENCE_TEXT_REPLACE_ACTION.ReferenceTextDisabled;

		public virtual string FormatUserQuestion(string userQuestion) => userQuestion;
		public virtual string GetCurrentReferenceText() => string.Empty;
		public abstract string GetSystemPrompt();
		/// <summary>
		/// This is the message header used at the start of a message to the AI providing some sort of reference text that will appear in a codeblock. For example "Users current webpage html"
		/// </summary>
		public virtual string ReferenceTextHeader => "Reference Text";
		public virtual string PlaceholderTextForReferenceTextRemoval => $"The old content for {ReferenceTextHeader} was here but changed. It has been removed to shorten history new version found later.";


		/// <summary>
		/// placeholder text and tooltip for the user input box on the ai chat
		/// </summary>
		public virtual string HintForUserInput => "Ask your question here...";
		/// <summary>
		/// If using reference text how should we refer to it as a user
		/// </summary>
		public virtual string ReferenceTextDisplayName => "Reference Text";
		public virtual int DefaultMaxReferenceTextCharsToSend => 5000;
		/// <summary>
		/// If we should ask user how many chars to send, with this disabled we only ask if they want to send reference text or not (assuming reference text is enabled)
		/// </summary>
		public virtual bool AllowUserToSetMaxReferenceTextCharsToSend => true;
		public virtual IEnumerable<CodeblockAction> CodeblockActions => [];
		/// <summary>
		/// Format the user question for the prompt.
		/// </summary>
		/// <param name="userQuestion"></param>
		/// <returns></returns>

		public virtual void HandleDebugMessage(string msg) { }
	}
}
