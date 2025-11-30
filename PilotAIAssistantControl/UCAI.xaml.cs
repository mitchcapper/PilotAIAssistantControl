using PilotAIAssistantControl.MVVM;

using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.ComponentModel;
using System.Diagnostics;

namespace PilotAIAssistantControl {


	public class UCAIDataContext : BaseNotifyObject {
		public bool ShowTokenConfig => SelectedPendingProvider?.CustomConfigControl == null;
		public ObservableCollection<UCAI.ChatItem> Messages { get; set => Set(ref field, value); } = new();
		public IAIModelProvider Provider { get; set => Set(ref field, value); }
		/// <summary>
		/// from combobox
		/// </summary>
		public IAIModelProvider SelectedPendingProvider {
			get; set {

				if (Set(ref field, value)) {
					RaisePropertyChanged(nameof(ShowTokenConfig));
				}

			}
		}


		/// <summary>
		/// The custom configuration control for the selected provider (if any).
		/// </summary>


		public AIOptions? Options {
			get; set {
				field = value;
				MaxReferenceTextCharsToSend = value?.DefaultMaxReferenceTextCharsToSend ?? 5000;
				RaisePropertyChanged(nameof(Options));  // always notify of changes if user explicitly sets incase they don't implement INotifyPropertyChanged themselves on something

			}
		}

		public bool SendReferenceText { get; set => Set(ref field, value); } = true;
		public int MaxReferenceTextCharsToSend { get; set => Set(ref field, value); }

		public bool ActiveTabIsSettingsTab => ActiveTab?.Header?.ToString().Contains("Settings") == true;
		public TabItem ActiveTab {
			get; set {
				var wasSettings = ActiveTabIsSettingsTab;
				if (Set(ref field, value)){
					if (wasSettings != ActiveTabIsSettingsTab){
						RaisePropertyChanged( () => ActiveTabIsSettingsTab);
						RaisePropertyChanged( ()=> SelectedPendingProvider);// important to make sure ui control is inited if it wasn't before
					}
				}

			}
		}

	}

	/// <summary>
	/// Interaction logic for UCAI.xaml
	/// </summary>
	public partial class UCAI : UserControl {



		public void Configure(AIOptions options) {
			if (vm.Options != null) {
				foreach (var provider in vm.Options.Providers)
					provider.StatusMessage -= Provider_StatusMessage;
			}
			vm.Options = options;
			foreach (var provider in vm.Options.Providers)
				provider.StatusMessage += Provider_StatusMessage;


		}

		// Chat message model with markdown support and colors
		public class ChatItem {
			public string Message { get; set; } = string.Empty;
			public string Sender { get; set; } = string.Empty;
			public Brush BackgroundColor { get; set; } = Brushes.White;
			public Brush SenderColor { get; set; } = Brushes.Gray;
			public HorizontalAlignment Alignment { get; set; } = HorizontalAlignment.Left;
			public List<CodeBlock> CodeBlocks { get; set; } = new();
			public bool HasCodeBlocks => CodeBlocks.Count > 0;



			public static ChatItem CreateUserMessage(string message) {
				return new ChatItem {
					Message = message,
					Sender = "You",
					BackgroundColor = new SolidColorBrush(Color.FromRgb(0xE3, 0xF2, 0xFD)), // Light blue
					SenderColor = new SolidColorBrush(Color.FromRgb(0x19, 0x76, 0xD2)),
					Alignment = HorizontalAlignment.Right
				};
			}
			private static Regex FindCodeBlockEnd = new(@"^```$", RegexOptions.Multiline);
			public static ChatItem CreateAiMessage(string message) {
				message = FindCodeBlockEnd.Replace(message, "\n```"); //bit hacky to make sure scrollbar doesn't make readability hard
				return new ChatItem {
					Message = message,
					Sender = "AI Assistant",
					BackgroundColor = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF5)), // Light gray
					SenderColor = new SolidColorBrush(Color.FromRgb(0x38, 0x8E, 0x3C)),
					Alignment = HorizontalAlignment.Left,
					CodeBlocks = CodeBlock.ExtractCodeBlocks(message)
				};
			}

			public static ChatItem CreateSystemMessage(string message, bool isError = false) {
				return new ChatItem {
					Message = message,
					Sender = "System",
					BackgroundColor = isError
						? new SolidColorBrush(Color.FromRgb(0xFF, 0xEB, 0xEE)) // Light red
						: new SolidColorBrush(Color.FromRgb(0xFF, 0xF8, 0xE1)), // Light amber
					SenderColor = isError ? Brushes.DarkRed : Brushes.DarkOrange,
					Alignment = HorizontalAlignment.Stretch
				};
			}
		}

		private readonly AiService _aiService = new AiService();

		private bool _hasAutoConnectedOnExpand = false; // Track if we've tried auto-connect on first expand

		// Message history for Up/Down arrow navigation (like VS Code)
		// _historyIndex == _messageHistory.Count means "current input", otherwise it's an index into history
		private readonly List<string> _messageHistory = new();
		private int _historyIndex = 0;
		private string _currentInput = string.Empty; // Stores current typing when navigating history

		public ObservableCollection<ChatItem> Messages => vm.Messages;
		public AIOptions Options => vm.Options;
		public UCAIDataContext vm = new();

		public UCAI() {
			DataContext = vm;
			InitializeComponent();
			UpdateConnectionStatus(false, default, default);
		}



		private void Provider_StatusMessage(object? sender, StatusMessageEventArgs e) {
			ShowStatus(e.Message, e.IsError);
		}


		private void ConfigControl_StatusMessage(object? sender, StatusMessageEventArgs e) {
			ShowStatus(e.Message, e.IsError);
		}

		#region Chat Tab Events

		private async void AskAi_Click(object sender, RoutedEventArgs e) => await SendToAi();

		private void ChatInput_PreviewKeyDown(object sender, KeyEventArgs e) {
			if (e.Key == Key.Up) {
				// Navigate to previous message if at first line
				if (IsCaretOnFirstLine() && _messageHistory.Count > 0) {
					e.Handled = true;
					NavigateHistory(-1);
				}
			} else if (e.Key == Key.Down) {
				// Navigate to next message if at last line and we're in history
				if (IsCaretOnLastLine() && _historyIndex < _messageHistory.Count) {
					e.Handled = true;
					NavigateHistory(1);
				}
			}
		}
		private async void ChatInput_KeyDown(object sender, KeyEventArgs e) {
			if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) {
				e.Handled = true;
				await SendToAi();
			}
		}

		/// <summary>
		/// Checks if the caret is on the first line of the TextBox.
		/// </summary>
		private bool IsCaretOnFirstLine() {
			var lineIndex = ChatInput.GetLineIndexFromCharacterIndex(ChatInput.CaretIndex);
			return lineIndex == 0;
		}

		/// <summary>
		/// Checks if the caret is on the last line of the TextBox.
		/// </summary>
		private bool IsCaretOnLastLine() {
			var lineIndex = ChatInput.GetLineIndexFromCharacterIndex(ChatInput.CaretIndex);
			var lastLineIndex = ChatInput.LineCount - 1;
			return lineIndex >= lastLineIndex;
		}

		/// <summary>
		/// Navigates through message history. Direction: -1 for older, +1 for newer.
		/// History index: Count = current input (not in history), Count-1 = most recent, 0 = oldest.
		/// </summary>
		private void NavigateHistory(int direction) {
			if (_messageHistory.Count == 0) return;

			// If starting fresh (at current input position), save what user typed
			if (_historyIndex == _messageHistory.Count && direction == -1) {
				_currentInput = ChatInput.Text;
			}

			// Calculate new index
			int newIndex = _historyIndex + direction;

			// Clamp: 0 (oldest) to Count (current input)
			if (newIndex < 0)
				newIndex = 0;
			else if (newIndex > _messageHistory.Count)
				newIndex = _messageHistory.Count;

			_historyIndex = newIndex;

			// Display appropriate text
			if (_historyIndex == _messageHistory.Count) {
				// Back to current input
				ChatInput.Text = _currentInput;
			} else {
				// Show history item
				ChatInput.Text = _messageHistory[_historyIndex];
			}

			// Move caret to end
			ChatInput.CaretIndex = ChatInput.Text.Length;
		}

		private void GoToSettings_Click(object sender, RoutedEventArgs e) => ourTabControl.SelectedIndex = 1;

		private void ClearChat_Click(object sender, RoutedEventArgs e) {
			// Clear UI messages
			Messages.Clear();

			// Clear conversation history in the service
			_aiService.ClearConversation();

			// Add a system message indicating chat was cleared
			AddMessage(ChatItem.CreateSystemMessage("Conversation cleared. Start a new chat!"));
		}

		private async Task SendToAi() {
			if (Options == null) {
				AddMessage(ChatItem.CreateSystemMessage("Not Initialized Backend Code Error", isError: true));
				return;
			}
			var question = ChatInput.Text;

			if (string.IsNullOrWhiteSpace(question)) return;
			_aiService.SetOptions(Options);
			_aiService.DebugAction = Options.HandleDebugMessage;
			if (!_aiService.IsConfigured) {
				AddMessage(ChatItem.CreateSystemMessage("Please configure the AI in the Settings tab first.", isError: true));
				return;
			}

			// Add to message history for Up/Down navigation
			_messageHistory.Add(question);
			_historyIndex = _messageHistory.Count; // Reset to "current input" position
			_currentInput = string.Empty;

			// Add user message
			AddMessage(ChatItem.CreateUserMessage(question));
			ChatInput.Text = "";

			// Add thinking indicator
			var thinkingMsg = ChatItem.CreateAiMessage("*Thinking...*");
			AddMessage(thinkingMsg);

			try {
				// Call the Service

				var response = await _aiService.GetSuggestionAsync(Options.FormatUserQuestion(question), CurTargetTextForAI());

				// Remove "Thinking..." and show response
				Messages.Remove(thinkingMsg);
				var msg = ChatItem.CreateAiMessage(response);
				foreach (var blk in msg.CodeBlocks)
					blk.Actions = Options.CodeblockActions.Where(a => a.IsVisible(blk)).Select(a => new BlockAction(a, blk)).ToArray();

				AddMessage(msg);
			} catch (Exception ex) {
				Messages.Remove(thinkingMsg);
				AddMessage(ChatItem.CreateSystemMessage($"Error: {ex.Message}", isError: true));
			}
		}

		private string CurTargetTextForAI() {
			if (ChkSendTargetText.IsChecked != true)
				return string.Empty;
			var curTextToSearch = Options.GetCurrentReferenceText();

			var maxlen = Int32.Parse(TxtTargetTextLength.Text);

			if (curTextToSearch.Length > maxlen)
				return curTextToSearch.Substring(0, maxlen);

			return curTextToSearch;
		}

		private void AddMessage(ChatItem message) {
			Messages.Add(message);
			// Scroll to bottom
			ChatScrollViewer.ScrollToEnd();
		}

		private async void CodeBlockAction_Click(object sender, RoutedEventArgs e) {
			if (sender is not Button button || button.DataContext is not BlockAction context)
				return;
			var minTime = TimeSpan.FromSeconds(1.5);
			var startTime = DateTime.Now;
			try {
				button.IsEnabled = false;
				button.Content = context.Action.FeedbackOnAction;
				await context.Action.DoAction(context.Block);
			} catch (Exception ex) {
				button.Content = "Failed: " + ex.Message;
			} finally {
				var timeWait = minTime - (DateTime.Now - startTime);
				if (timeWait.TotalSeconds > 0)
					await Task.Delay(timeWait);
				button.IsEnabled = true;
				button.Content = context.Action.DisplayName;
			}
		}



		#endregion

		#region Settings Tab Events

		private void ComboProvider_SelectionChanged() {
			if (ControlLoadedAndDataImported?.Task.IsCompleted != true)
				return;

			if (!vm.ActiveTabIsSettingsTab)
				return;

			var selected = vm.SelectedPendingProvider;
			if (selected == null) return;

			// Create custom config control if available
			if (selected.CustomConfigControl != null && selected.ControlInstance == null) {
				selected.ControlInstance = selected.CustomConfigControl?.Invoke();
				selected.ControlInstance.DataContext = selected;
			}
			TxtApiKey.Password = selected.UserData.Token;

			selected.ProviderSelected();



		}

		private bool DataImported;

		private TaskCompletionSource<object> ControlLoadedAndDataImported = new();

		private void RefreshModels_Click(object sender, RoutedEventArgs e) {
			var provider = vm.SelectedPendingProvider;
			if (provider == null) return;

			BtnRefreshModels.IsEnabled = false;
			BtnRefreshModels.Content = "↻ Loading...";

			try {
				provider.RefreshModels();
			} finally {
				BtnRefreshModels.IsEnabled = true;
				BtnRefreshModels.Content = "↻ Refresh";
			}
		}

		private async void TestConnection_Click(object sender, RoutedEventArgs e) {
			var provider = vm.SelectedPendingProvider;

			if (provider == null) {
				ShowStatus("Please select a provider", isError: true);
				return;
			}

			var token = provider.UserData.Token;

			if (string.IsNullOrEmpty(token) && provider.TokenRequired) {
				ShowStatus("Please enter a token/API key", isError: true);
				return;
			}

			BtnTestConnection.IsEnabled = false;
			BtnTestConnection.Content = "🔗 Testing...";

			try {
				var testToken = await provider.GetTokenForConnect();
				bool isValid = !string.IsNullOrEmpty(testToken);

				ShowStatus(isValid
					? $"✓ Connection to {provider} successful!"
					: $"✗ Connection failed. Please check your credentials.",
					isError: !isValid);
			} catch (Exception ex) {
				ShowStatus($"Connection error: {ex.Message}", isError: true);
			} finally {
				BtnTestConnection.IsEnabled = true;
				BtnTestConnection.Content = "🔗 Test Connection";
			}
		}

		/// <summary>
		/// switch pending to actual
		/// </summary>
		private async void SaveSettings_Click(object sender, RoutedEventArgs e) {
			try {
				var provider = vm.SelectedPendingProvider;

				if (provider == null) {
					ShowStatus("Please select a provider", isError: true);
					return;
				}



				if (string.IsNullOrWhiteSpace(provider.UserData.ModelId))
					throw new ArgumentException("ModelID cannot be null");



				// Call GetTokenForConnect - provider handles token exchange
				ShowStatus("Connecting...", isError: false);
				string apiKey = await provider.GetTokenForConnect();
				if (string.IsNullOrWhiteSpace(apiKey) && provider.TokenRequired)
					throw new ArgumentException("Token required and not provided");
				vm.Provider = provider;

				_aiService.Configure(provider, apiKey);


				UpdateConnectionStatus(true, provider.Name, provider.UserData.ModelId);

				ourTabControl.SelectedIndex = 0;
			} catch (Exception ex) {
				ShowStatus($"Configuration Error: {ex.Message}", isError: true);
				UpdateConnectionStatus(false, null, null);
			}
		}

		private void ShowStatus(string message, bool isError) {
			if (isError) {
				StatusBorder.Visibility = Visibility.Collapsed;
				ErrorBorder.Visibility = Visibility.Visible;
				LblError.Text = message;
			} else {
				ErrorBorder.Visibility = Visibility.Collapsed;
				StatusBorder.Visibility = Visibility.Visible;
				LblStatus.Text = message;
			}
		}

		private void UpdateConnectionStatus(bool connected, string? provider, string? model) {
			if (!String.IsNullOrWhiteSpace(provider))
				ShowStatus($"✓ Connected to {provider} using {vm.Provider.UserData.ModelId}!", isError: false);
			if (connected) {
				TxtCurrentModel.Foreground = new SolidColorBrush(Colors.Green);
				TxtCurrentModel.Text = $"{provider}: {model}";
			} else {
				TxtCurrentModel.Foreground = new SolidColorBrush(Colors.Gray);
				TxtCurrentModel.Text = "Not Connected";
			}
		}

		#endregion

		#region Data Export/Import

		/// <summary>
		/// Exports the current AI settings to be saved with application data.
		/// </summary>
		public AIUserConfig ExportData() {
			var data = new AIUserConfig();
			if (vm.Provider != null)
				data.ProviderId = vm.Provider.Id;

			foreach (var provider in vm.Options.Providers) {
				// Save provider-specific custom data
				var customData = provider.SaveData();
				if (customData != null && customData?.Type != Newtonsoft.Json.Linq.JTokenType.Null)
					data.ProviderToProviderData[provider.Id] = customData;

			}

			return data;
		}

		/// <summary>
		/// Imports saved AI settings and restores the UI state.
		/// </summary>
		public void ImportData(AIUserConfig? data) {
			if (Options == null)
				throw new InvalidOperationException($"Must call {nameof(Configure)} before {nameof(ImportData)}");

			if (data == null) return;

			foreach (var provider in vm.Options.Providers) {
				JToken? customData = null;
				data.ProviderToProviderData.TryGetValue(provider.Id, out customData);

				// Call LoadData on provider (lightweight)
				provider.LoadData(customData);
			}


			vm.SelectedPendingProvider = vm.Options.Providers.FirstOrDefault(p => p.Id == data.ProviderId);
			if (vm.SelectedPendingProvider == null)
				vm.SelectedPendingProvider = vm.Options.Providers.First();


			DataImported = true;
			CheckForLoadCompletion();

		}

		private async void CheckForLoadCompletion() {
			if (IsLoaded && DataImported && ControlLoadedAndDataImported?.Task?.IsCompleted == false) { //we dont need to check if configured as ImportData throws an exception if not configured
				lock (ControlLoadedAndDataImported) {
					if (ControlLoadedAndDataImported?.Task?.IsCompleted == true)
						return;
					ControlLoadedAndDataImported.TrySetResult(null!);
				}
				//ComboProvider_SelectionChanged(default!, default!);
				vm.RaisePropertyChanged(() => vm.SelectedPendingProvider);

			}

		}

		/// <summary>
		/// Called when the AI panel is expanded for the first time.
		/// Attempts to auto-connect if autodiscover is enabled or if a token is available.
		/// </summary>
		public async void TryAutoConnectOnExpand() {

			if (_hasAutoConnectedOnExpand)
				return;

			_hasAutoConnectedOnExpand = true;
			await Task.WhenAny(Task.Delay(5000), ControlLoadedAndDataImported.Task);
			if (vm.SelectedPendingProvider?.UserData?.ModelId != null)
				SaveSettings_Click(default, default);


		}


		#endregion
		private bool isFirstLoad = true;
		private void UserControl_Loaded(object sender, RoutedEventArgs e) {
			CheckForLoadCompletion();
			if (isFirstLoad)
				vm.PropertyChanged += vmPropChanged;
			isFirstLoad = false;
		}

		private void vmPropChanged(object? sender, PropertyChangedEventArgs e) {
			if (e.PropertyName == nameof(vm.SelectedPendingProvider))
				ComboProvider_SelectionChanged();
		}

		private void TxtApiKey_PasswordChanged(object sender, RoutedEventArgs e) {
			vm.SelectedPendingProvider?.UserData.Token = TxtApiKey.Password;
		}

	}
}
