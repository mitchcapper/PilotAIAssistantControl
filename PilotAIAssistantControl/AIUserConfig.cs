using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace PilotAIAssistantControl {
	/// <summary>
	/// Serializable use configuration items that can be imported/exported
	/// </summary>
	public class AIUserConfig {
		/// <summary>
		/// The selected provider tag (e.g., "copilot", "openai", "github-models", "ollama")
		/// </summary>
		public string? ProviderId { get; set; }


		public Dictionary<string, JToken> ProviderToProviderData {get;set; } = new();

	
	}
}
