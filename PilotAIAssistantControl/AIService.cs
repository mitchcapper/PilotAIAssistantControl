using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

namespace PilotAIAssistantControl {
	public class AiService {


		private IChatCompletionService? _chatService;
		private Kernel? _kernel;
		private HttpClient? _httpClient; // Keep reference to avoid disposal
		private ChatHistory _chatHistory = new(); // Persistent conversation history
		public bool IsConfigured => _chatService != null;

		public string? CurrentProvider { get; private set; }
		public string? CurrentModelId { get; private set; }
		public AIOptions Options { get; private set; }

		public Action<string>? DebugAction;


		public void Configure(IAIModelProvider provider, String apiKey) {
			// Dispose previous HttpClient if any
			_httpClient?.Dispose();
			_httpClient = null;

			var builder = Kernel.CreateBuilder();
			_httpClient = new HttpClient();
			foreach (var header in provider.HTTPHeadersToAdd)
				_httpClient.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);

			builder.AddOpenAIChatCompletion(
				modelId: provider.UserData.ModelId,
				apiKey: apiKey,
				httpClient: _httpClient,
				endpoint: string.IsNullOrWhiteSpace(provider.UserData.Endpoint) ? null : new Uri(provider.UserData.Endpoint)
			);



			_kernel = builder.Build();
			_chatService = _kernel.GetRequiredService<IChatCompletionService>();

			// Initialize fresh chat history with system prompt
			_chatHistory = new ChatHistory();
			lastTargetText = string.Empty;
			SetSystemPromptIfChanged(true);
		}
		private string? lastPrompt;
		private void SetSystemPromptIfChanged(bool force = false) {
			var curPrompt = GetSystemPrompt();
			if (curPrompt == lastPrompt && !force)
				return;
			var sysInfo = _chatHistory.FirstOrDefault(a => a.Role == AuthorRole.System);
			if (sysInfo != null)
				_chatHistory.Remove(sysInfo);
			_chatHistory.AddSystemMessage(curPrompt);
			lastPrompt = curPrompt;
		}

		private string GetSystemPrompt() => Options?.GetSystemPrompt() ?? string.Empty;

		public async Task<string> GetSuggestionAsync(string userPrompt, string ReferenceText) {
			if (_chatService == null)
				return "Error: AI Service not configured. Please go to Settings.";

			SetSystemPromptIfChanged();
			// Build the user message with current context
			SetOrUpdateTargetTextIfChanged(ReferenceText);


			// Add user message to history
			_chatHistory.AddUserMessage(userPrompt);


			if (DebugAction != null) {
				StringBuilder debugSb = new StringBuilder();
				debugSb.AppendLine("=== AI Chat History ===");
				foreach (var message in _chatHistory) {
					debugSb.AppendLine($"[{message.Role}] {message.Content}");
				}
				debugSb.AppendLine("=======================");
				DebugAction?.Invoke(debugSb.ToString());
			}
			// Get response
			var result = await _chatService.GetChatMessageContentAsync(_chatHistory);
			var response = result.Content ?? "No response from AI.";

			DebugAction?.Invoke($"[AI Response] {response}");

			// Add assistant response to history for context in follow-up questions
			_chatHistory.AddAssistantMessage(response);

			return response;
		}

		private string lastTargetText = string.Empty;
		private const string REFERENCE_TEXT_CODEBLOCK_DELIM = "\n```\n";
		/// <summary>
		/// Sadly right now developer or tool both throw an error...
		/// </summary>
		private AuthorRole StoreReferenceTextUnder = AuthorRole.User;
		private void SetOrUpdateTargetTextIfChanged(string targetText) {
			if (lastTargetText == targetText || Options.ReplaceAction == AIOptions.REFERENCE_TEXT_REPLACE_ACTION.ReferenceTextDisabled)
				return;
			lastTargetText = targetText;
			var PreTarget = $"{Options.ReferenceTextHeader}:{REFERENCE_TEXT_CODEBLOCK_DELIM}";
			var msgStr = $"{PreTarget}{targetText}{REFERENCE_TEXT_CODEBLOCK_DELIM}\n";
			var curMsg = _chatHistory.FirstOrDefault(x => x.Role == StoreReferenceTextUnder && x.Content?.StartsWith(PreTarget) == true);

			if (curMsg != null) {
				switch (Options.ReplaceAction) {
					case AIOptions.REFERENCE_TEXT_REPLACE_ACTION.UpdateInPlace:
						curMsg.Content = msgStr;
						return; // Don't add a new message

					case AIOptions.REFERENCE_TEXT_REPLACE_ACTION.ChangeOldToPlaceholder:
						curMsg.Content = Options.PlaceholderTextForReferenceTextRemoval;
						break;

					case AIOptions.REFERENCE_TEXT_REPLACE_ACTION.LeaveOldInplace:
						break;

					case AIOptions.REFERENCE_TEXT_REPLACE_ACTION.DeleteOld:
						_chatHistory.Remove(curMsg);
						break;
				}
			}

			// Add the new reference text message
			_chatHistory.AddMessage(StoreReferenceTextUnder, content: msgStr);

		}

		/// <summary>
		/// Clears the conversation history while keeping the system prompt.
		/// Call this when starting a new conversation.
		/// </summary>
		public void ClearConversation() {
			if (_chatHistory != null) {
				_chatHistory.Clear();
				lastTargetText = string.Empty;
				SetSystemPromptIfChanged(true);
			}
		}

		internal void SetOptions(AIOptions options) {
			this.Options = options;
		}

		public void Reset() {
			_chatService = null;
			_kernel = null;
			_chatHistory = new();
			_httpClient?.Dispose();
			_httpClient = null;
			CurrentProvider = null;
			CurrentModelId = null;
		}
	}
}

#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
