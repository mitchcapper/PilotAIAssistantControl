using Newtonsoft.Json.Linq;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

namespace PilotAIAssistantControl {
	public class CoPilotUserData : IAIModelProvider.GenericProviderUserData {
		public bool AutoDiscover { get; set; }
	}
	/// <summary>
	/// GitHub Copilot provider with auto-discovery and token exchange.
	/// </summary>
	public class GithubCopilotProvider : BaseAiModelProvider<CoPilotUserData> {

		/// <summary>
		/// shortlived session token acquired using oauth token
		/// </summary>
		private CopilotTokenHelper.CopilotApiToken? SessionToken;

		protected override void SetDefaultUserData() {
			base.SetDefaultUserData();
			UserData.AutoDiscover = true;
		}

		public bool AutoDiscover {
			get; set => ChangedIf(Set(ref field, value), () => TokenHelp, () => ShowAPIKeyField);
		}
		public override bool ShowAPIKeyField { get => !AutoDiscover; set => throw new NotImplementedException(); }
		public override string TokenHelp {
			get => AutoDiscover ? "Token will be auto-discovered from IDE config files" : "Enter your API token manually or use sign-in";
			set => throw new NotImplementedException();

		}
		public GithubCopilotProvider() : base() {
			Id = "GithubCopilot";
			Name = "GitHub CoPilot";
			Description = "Uses your existing GitHub Copilot subscription. Token auto-discovered from IDE configs.";
			TokenHint = "OAuth Token";
			ModelHint = "Click 'Refresh' to discover models from your Copilot subscription";
			HTTPHeadersToAdd = CopilotTokenHelper.HeadersToAdd.ToList();
			AvailableModels = new ObservableCollection<IAIModelProvider.IModel>();
			CustomConfigControl = () => new UCConfigureCopilot();

		}

		public override void LoadData(JToken? data) {
			base.LoadData(data);
			AutoDiscover = UserData.AutoDiscover;
		}

		public override JToken SaveData() {
			UserData.AutoDiscover = AutoDiscover;
			var ret = base.SaveData();
			if (AutoDiscover)
				ret[nameof(CoPilotUserData.Token)] = null; // Don't save token if auto-discover is enabled
			return ret;
		}

		public override async void ProviderSelected() {
			if (AutoDiscover && string.IsNullOrEmpty(UserData.Token))
				await PerformAutoDiscover();
			if (AvailableModels.Count == 0)
				await LoadModelsFromApi();
		}
		/// <summary>
		/// Loads available models from the Copilot API.
		/// </summary>
		public async Task LoadModelsFromApi() {

			try {
				await EnsureValidSessionToken();
				var result = await CopilotTokenHelper.FetchAvailableModelsAsync(SessionToken);
				var models = AvailableModels;
				models.Clear();

				if (result.Success && result.Models.Count > 0) {
					foreach (var model in result.Models)
						models.Add(model);
					RaiseStatusMessage($"✓ Discovered {result.Models.Count} available models", isError: false);
					SelectedModel = models.FirstOrDefault(a => a.Id == UserData.ModelId) ?? models.FirstOrDefault();
				} else if (!result.Success) {
					SessionToken = null;
					RaiseStatusMessage(result.ErrorMessage ?? "Failed to fetch models", isError: true);
				} else {
					RaiseStatusMessage("No models available in your subscription", isError: true);
				}
			} catch (Exception ex) {
				AvailableModels?.Clear();
				RaiseStatusMessage($"Error: {ex.Message}", isError: true);
			}
		}

		private async Task EnsureValidSessionToken() {
			var res = await CopilotTokenHelper.EnsureValidSessionToken(UserData.Token, SessionToken);
			if (!res.Success)
				throw new Exception(res.ErrorMessage);
			else{
				SessionToken = res.ApiToken;
				UserData.Endpoint = SessionToken.ApiEndpoint;
			}
		}

		public override async void RefreshModels() => await LoadModelsFromApi();

		/// <summary>
		/// Performs token auto-discovery and triggers model loading if successful.
		/// </summary>
		public async Task PerformAutoDiscover() {
			RaiseStatusMessage("🔍 Auto-discovering token...", isError: false);
			SessionToken = null;
			string? foundToken = await Task.Run(() => CopilotTokenHelper.DiscoverToken());
			var tokenFound = !string.IsNullOrEmpty(foundToken);
			UserData.Token = foundToken;
			AutoDiscoverResult = tokenFound ? "Found Token" : "Token Not Found";

			if (tokenFound) {
				RaiseStatusMessage("✓ Token auto-discovered! You can now connect.", isError: false);

			} else {
				RaiseStatusMessage("Token not found. Try unchecking autodiscover and hit 'Sign In'.", isError: true);
			}
		}

		public string AutoDiscoverResult {
			get; set => Set(ref field, value);
		}

		public override async Task<string> GetTokenForConnect() {
			// Heavy work happens HERE, not in LoadData
			string oauthToken = UserData.Token;

			// Auto-discover if enabled and no token provided
			if (AutoDiscover && string.IsNullOrEmpty(UserData.Token))
				await PerformAutoDiscover();

			if (string.IsNullOrEmpty(UserData.Token))
				throw new InvalidOperationException("No OAuth token available. Please provide a token or enable auto-discovery.");


			// Exchange OAuth token for API token
			RaiseStatusMessage("Exchanging OAuth token for API token...", false);
			await EnsureValidSessionToken();

			return SessionToken.Token;
		}
	}
}
