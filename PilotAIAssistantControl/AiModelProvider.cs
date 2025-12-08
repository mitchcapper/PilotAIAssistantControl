using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using PilotAIAssistantControl.MVVM;

#if WPF
using System.Windows.Controls;
#else
using Microsoft.UI.Xaml.Controls;
#endif

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

namespace PilotAIAssistantControl {
	/// <summary>
	/// Event args for StatusMessage event.
	/// </summary>
	public class StatusMessageEventArgs : EventArgs {
		public string Message { get; }
		public bool IsError { get; }

		public StatusMessageEventArgs(string message, bool isError) {
			Message = message;
			IsError = isError;
		}
	}
	public interface IAIModelProvider : INotifyPropertyChanged {
		public interface IModel {
			string DisplayName { get; }
			string Id { get; }
			string Tooltip { get; }
		}
		public class SimpleModel : IModel {
			public string Id { get; set; }
			public string DisplayName { get; set; }
			public string Tooltip { get; set; }
			public long? Created { get; set; }
		}
		public interface IUserData {
			string Endpoint { get; set; }
			string ModelId { get; set; }
			string Token { get; set; }
			string ModelsListEndpoint { get; set; }
		}
		public class GenericProviderUserData : BaseNotifyObject, IUserData {
			public string Endpoint { get; set; }
			public string ModelId { get; set; }
			public string Token { get; set; }

			public string? ModelsListEndpoint {
				get; set => Set(ref field, value);
			}


		}
		public static IAIModelProvider[] DefaultProviders = [
			new GithubCopilotProvider(),

			new GenericAIModelProvider(){Id="GithubModel",Name="GitHub Models",Description="Uses GitHub Models with your GitHub PAT (Personal Access Token)",
				TokenHint="GitHub PAT",
				TokenHelp="Create a PAT at github.com/settings/tokens with 'models:read' scope",
				ModelHint="Enter model name (e.g., gpt-4o, gpt-4o-mini)",
				DefaultModelId="gpt-4o",
				DefaultEndpoint="https://models.inference.ai.azure.com",
			},
			new GenericAIModelProvider(){Id="OpenAI",Name="OpenAI",Description="Uses OpenAI API directly with your API key",
				TokenHint="OpenAI API Key",
				TokenHelp="Get your API key from platform.openai.com/api-keys",
				ModelHint="Enter model name (e.g., gpt-4o, gpt-4-turbo, gpt-3.5-turbo)",
				DefaultModelId="gpt-4o",
				DefaultEndpoint="https://api.openai.com/v1",
			},
			new GenericAIModelProvider() {Id="ollama",Name="Local / Custom Endpoint(Ollama)", Description="Connects to a custom OpenAI endpoint instance",
				TokenHint="API Key (optional)",
				TokenHelp="Ollama generally ignores the API key so it isn't needed",
				DefaultModelId="llama3",
				ModelHint="Enter model name (e.g., llama3, codellama, mistral)",
				DefaultEndpoint="http://localhost:11434/v1",
				AllowEndpointCustomization=true,
				TokenRequired=false,

			}


		   ];
		IModel? SelectedModel { get; set; }
		ObservableCollection<IModel>? AvailableModels { get;set; }
		string Id { get; }
		IUserData UserData { get; }
		string Name { get; }
		string Description { get; }
		string ModelHint { get; }
		string TokenHint { get; }
		string TokenHelp { get; }
		string EndpointHint { get; }
		
		bool AllowEndpointCustomization { get; }
		bool ShowAPIKeyField {get;}
		bool TokenRequired { get; }
		List<KeyValuePair<string, string>> HTTPHeadersToAdd { get; }
		event EventHandler<StatusMessageEventArgs>? StatusMessage;

		/// <summary>Called on app startup to load provider-specific custom data. Should NOT do heavy work.</summary>
		void LoadData(JToken? data);

		/// <summary>Called when settings are saved. Returns provider-specific custom data to persist.</summary>
		JToken SaveData();

		/// <summary>Called when user selects this provider in settings (NOT on startup). Should populate AvailableModels if applicable.</summary>
		void ProviderSelected();

		/// <summary>Called when user clicks Refresh Models button. Should re-populate AvailableModels.</summary>
		void RefreshModels();

		/// <summary>Called when user clicks Connect button. Provider can do work here (exchange tokens, auto-discover, etc.). Returns final token to use.</summary>
		Task<string> GetTokenForConnect();

		/// <summary>Factory function to create a custom configuration control for this provider. The control will have the Provider set as its DataContext. Return null to use the default token input UI.</summary>
		Func<UserControl>? CustomConfigControl { get; }

		UserControl? ControlInstance { get;set; }
		string DefaultModelId { get; }
		string DefaultEndpoint { get; }
		string DefaultModelListEndpoint { get; }
	}
	

	public class GenericAIModelProvider : BaseAiModelProvider<IAIModelProvider.GenericProviderUserData> {
		public GenericAIModelProvider() : base() {
			AvailableModels = new ObservableCollection<IAIModelProvider.IModel>();
		}

		public override void ProviderSelected() {
			if (AvailableModels?.Count == 0 && ! String.IsNullOrWhiteSpace(UserData.ModelsListEndpoint))
				RefreshModels();
		}

		public override async void RefreshModels() {
			if (string.IsNullOrWhiteSpace(UserData.ModelsListEndpoint)) {
				RaiseStatusMessage("No models endpoint configured. Enter model name manually.", isError: false);
				return;
			}

			if (TokenRequired && string.IsNullOrWhiteSpace(UserData.Token)) {
				RaiseStatusMessage("API key required. Please enter your API key first.", isError: true);
				return;
			}

			if (string.IsNullOrWhiteSpace(UserData.Endpoint)) {
				RaiseStatusMessage("Endpoint required. Please configure the endpoint.", isError: true);
				return;
			}

			try {
				var result = await ModelFetchHelper.FetchOpenAICompatibleModelsAsync(
					 GetFullModelListEndpoint(),
					UserData.Token,
					HTTPHeadersToAdd);

				AvailableModels?.Clear();

				if (result.Success && result.Models.Count > 0) {
					foreach (var model in result.Models)
						AvailableModels?.Add(model);
					RaiseStatusMessage($"✓ Discovered {result.Models.Count} available models", isError: false);
					if (AvailableModels != null)
						SelectedModel = AvailableModels.FirstOrDefault(a => a.Id == UserData.ModelId) ?? AvailableModels.FirstOrDefault();
				} else if (!result.Success) {
					RaiseStatusMessage(result.ErrorMessage ?? "Failed to fetch models", isError: true);
				} else {
					RaiseStatusMessage("No models available", isError: true);
				}
			} catch (Exception ex) {
				AvailableModels?.Clear();
				RaiseStatusMessage($"Error: {ex.Message}", isError: true);
			}
		}

		private string GetFullModelListEndpoint() => UserData.ModelsListEndpoint?.Contains("://") == true ? UserData.ModelsListEndpoint : UserData.Endpoint + UserData.ModelsListEndpoint;
	}
	public class BaseAiModelProvider<USER_DATA_TYPE> :  MVVM.BaseNotifyObject, IAIModelProvider where USER_DATA_TYPE : class, IAIModelProvider.IUserData, new() {
		public BaseAiModelProvider() {
			UserData=new();
			SetDefaultUserData();
		}

		/// <summary>Encrypts data using DPAPI for current user.</summary>
		protected static string? EncryptData(string? data) {
			if (string.IsNullOrEmpty(data))
				return data;
			try {
				var bytes = Encoding.UTF8.GetBytes(data);
				var encrypted = ProtectedData.Protect(bytes, s_additionalEntropy, DataProtectionScope.CurrentUser);
				return Convert.ToBase64String(encrypted);
			} catch {
				return data; // Return original if encryption fails
			}
		}
		static byte[] s_additionalEntropy = [0x50, 0x69, 0x6C, 0x6F, 0x74, 0x41, 0x49, 0x41, 0x73, 0x73, 0x69, 0x73, 0x74, 0x61, 0x6E, 0x74, 0x23, 0x40, 0x43, 0x6F, 0x6E, 0x74, 0x72, 0x6F, 0x6C, 0x45, 0x6E, 0x74, 0x72, 0x6F, 0x70, 0x79];
		/// <summary>Decrypts data using DPAPI for current user.</summary>
		protected static string? DecryptData(string? data) {
			if (string.IsNullOrEmpty(data))
				return data;
			try {
				var encrypted = Convert.FromBase64String(data);
				var bytes = ProtectedData.Unprotect(encrypted, s_additionalEntropy, DataProtectionScope.CurrentUser);
				return Encoding.UTF8.GetString(bytes);
			} catch {
				return data; // Return original if decryption fails (might be unencrypted legacy data)
			}
		}
		public virtual bool TokenRequired { get; set; } = true;
		protected virtual void SetDefaultUserData() {
			UserData.Endpoint = DefaultEndpoint;
			UserData.ModelId = DefaultModelId;
			UserData.ModelsListEndpoint = DefaultEndpoint;
		}

		public IAIModelProvider.IModel? SelectedModel {
			get; set {
				if (Set(ref field, value) && value != null)
					UserData.ModelId = value.Id;

			}
		}


		public string Id { get; set; }

		public virtual USER_DATA_TYPE UserData {
			get; set => Set(ref field, value);
		}

		public string Name { get; set; }
		public string Description { get; set; }
		public string ModelHint { get; set; }
		public string TokenHint { get; set; }
		public virtual string TokenHelp { get; set; }
		public string EndpointHint { get; set; } = "ie: http://localhost:11434/v1";

		public bool AllowEndpointCustomization { get; set; }


		public virtual bool ShowAPIKeyField {
			get; set => Set(ref field, value);
		} = true;



		public List<KeyValuePair<string, string>> HTTPHeadersToAdd { get; set; } = new();
		public ObservableCollection<IAIModelProvider.IModel>? AvailableModels { get; set; }

		public event EventHandler<StatusMessageEventArgs>? StatusMessage;

		public virtual void LoadData(JToken? data){

			UserData = data?.ToObject<USER_DATA_TYPE>() ?? UserData;
			
			// Decrypt token if present
			UserData.Token = DecryptData(UserData.Token) ?? string.Empty;
			
			if (! AllowEndpointCustomization) {
				UserData.Endpoint = DefaultEndpoint;
				UserData.ModelsListEndpoint = DefaultModelListEndpoint;
			}else{
				if (UserData.ModelsListEndpoint == null) //null but not empty
					UserData.ModelsListEndpoint = DefaultModelListEndpoint;
			}

			if (String.IsNullOrWhiteSpace( UserData.ModelId))
				SetDefaultUserData();

		}

		public virtual JToken SaveData() {
			var dataToSave = JObject.FromObject(UserData);
			
			// Encrypt token before saving
			if (!string.IsNullOrEmpty(UserData.Token))
				dataToSave[nameof(UserData.Token)] = EncryptData(UserData.Token);
			
			return dataToSave;
		}

		public virtual void ProviderSelected() { }

		public virtual void RefreshModels() { }

		public virtual Task<string> GetTokenForConnect() => Task.FromResult(UserData.Token);

		/// <summary>Helper method for custom controls to raise status messages.</summary>
		public void RaiseStatusMessage(string message, bool isError = false) {
			StatusMessage?.Invoke(this, new StatusMessageEventArgs(message, isError));
		}

		public Func<UserControl>? CustomConfigControl { get; set; }


		public UserControl? ControlInstance {
			get; set => Set(ref field, value);
		}
		public string DefaultModelId { get; set; }
		public string DefaultEndpoint { get; set; }
		public string DefaultModelListEndpoint { get; set; }  = "/models";
		IAIModelProvider.IUserData IAIModelProvider.UserData => UserData;

		override public string ToString() => Name;
	}
}
