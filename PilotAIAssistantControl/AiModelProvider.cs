using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Controls;

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
		public interface IUserData {
			string Endpoint { get; set; }
			string ModelId { get; set; }
			string Token { get; set; }
		}
		public class GenericProviderUserData : IUserData {
			public string Endpoint { get; set; }
			public string ModelId { get; set; }
			public string Token { get; set; }

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
			},
			new GenericAIModelProvider() {Id="ollama",Name="Local (Ollama)", Description="Connects to a local Ollama instance",
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
		ObservableCollection<IModel>? AvailableModels { get; }
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
	}
	public class GenericAIModelProvider : BaseAiModelProvider<IAIModelProvider.GenericProviderUserData> {

	}
	public class BaseAiModelProvider<USER_DATA_TYPE> :  MVVM.BaseNotifyObject, IAIModelProvider where USER_DATA_TYPE : class, IAIModelProvider.IUserData, new() {
		public BaseAiModelProvider() {
			UserData=new();
			SetDefaultUserData();
		}
		public virtual bool TokenRequired { get; set; } = true;
		protected virtual void SetDefaultUserData() {
			UserData.Endpoint = DefaultEndpoint;
			UserData.ModelId = DefaultModelId;
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
			if (! AllowEndpointCustomization)
				UserData.Endpoint = DefaultEndpoint;
			if (String.IsNullOrWhiteSpace( UserData.ModelId))
				SetDefaultUserData();

		}

		public virtual JToken SaveData() => JToken.FromObject(UserData);

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
		IAIModelProvider.IUserData IAIModelProvider.UserData => UserData;

		override public string ToString() => Name;
	}
}
