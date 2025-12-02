using Newtonsoft.Json.Linq;
using System.Collections.Generic;

using System;
using System.Text.Json;
using System.Text.Json.Serialization;


namespace PilotAIAssistantControl {
	/// <summary>
	/// Serializable use configuration items that can be imported/exported
	/// </summary>
	public class AIUserConfig {
		/// <summary>
		/// The selected provider tag (e.g., "copilot", "openai", "github-models", "ollama")
		/// </summary>
		public string? ProviderId { get; set; }

		[JsonConverter(typeof(DictionaryJTokenConverter))]
		public Dictionary<string, JToken> ProviderToProviderData { get; set; } = new();


	}

	public class DictionaryJTokenConverter : JsonConverter<Dictionary<string, JToken>> {
		private readonly JTokenConverter _jTokenConverter = new JTokenConverter();

		public override Dictionary<string, JToken> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
			if (reader.TokenType != JsonTokenType.StartObject)
				throw new JsonException();

			var dictionary = new Dictionary<string, JToken>();

			while (reader.Read()) {
				if (reader.TokenType == JsonTokenType.EndObject)
					return dictionary;

				// Get the key
				if (reader.TokenType != JsonTokenType.PropertyName)
					throw new JsonException();

				string key = reader.GetString();

				// Move to the value
				reader.Read();

				// Use your existing logic to convert the value
				var jToken = (JToken)_jTokenConverter.Read(ref reader, typeof(JToken), options);
				dictionary.Add(key, jToken);
			}

			throw new JsonException();
		}

		public override void Write(Utf8JsonWriter writer, Dictionary<string, JToken> value, JsonSerializerOptions options) {
			writer.WriteStartObject();

			foreach (var kvp in value) {
				writer.WritePropertyName(kvp.Key);
				// Use your existing logic to write the value
				if (kvp.Value != null) {
					_jTokenConverter.Write(writer, kvp.Value, options);
				} else {
					writer.WriteNullValue();
				}
			}

			writer.WriteEndObject();
		}
	}

	public class JTokenConverter : JsonConverter<JToken> {
		public override JToken Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
			// 1. Use JsonDocument to parse the current JSON structure from System.Text.Json
			using var jsonDoc = JsonDocument.ParseValue(ref reader);

			// 2. Get the raw JSON string
			var rawJson = jsonDoc.RootElement.GetRawText();

			// 3. Parse that string into a JToken using Newtonsoft
			return JToken.Parse(rawJson);
		}

		public override void Write(Utf8JsonWriter writer, JToken value, JsonSerializerOptions options) {
			// 1. Convert the JToken to a JSON string
			var jsonString = value.ToString(Newtonsoft.Json.Formatting.None);

			// 2. Write that raw JSON string directly to the System.Text.Json writer
			// Note: WriteRawValue is available in .NET 6+. 
			// For older versions, you'd need to parse the string into a JsonDocument and write that.
			writer.WriteRawValue(jsonString);
		}
	}
}
