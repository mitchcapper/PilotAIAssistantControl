using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace PilotAIAssistantControl {
	public static class ModelFetchHelper {
		public class FetchModelsResult {
			public bool Success { get; set; }
			public List<IAIModelProvider.SimpleModel> Models { get; set; } = new();
			public string? ErrorMessage { get; set; }
		}

		public static async Task<FetchModelsResult> FetchOpenAICompatibleModelsAsync(
			string modelsEndpoint,
			string? apiToken,
			List<KeyValuePair<string, string>>? additionalHeaders = null) {

			var result = new FetchModelsResult();

			try {
				using var client = new HttpClient();
				client.Timeout = TimeSpan.FromSeconds(30);

				using var request = new HttpRequestMessage(HttpMethod.Get, modelsEndpoint);

				if (!string.IsNullOrWhiteSpace(apiToken))
					request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

				if (additionalHeaders != null) {
					foreach (var header in additionalHeaders)
						request.Headers.TryAddWithoutValidation(header.Key, header.Value);
				}

				var response = await client.SendAsync(request);

				if (!response.IsSuccessStatusCode) {
					result.ErrorMessage = response.StatusCode switch {
						HttpStatusCode.Unauthorized => "API key is invalid or expired. Please check your credentials.",
						HttpStatusCode.Forbidden => "Access denied. Please verify your API key has the required permissions.",
						HttpStatusCode.NotFound => "Models endpoint not found. Please check the endpoint URL.",
						_ => $"API error: {response.StatusCode} - {response.ReasonPhrase}"
					};
					return result;
				}

				var responseBody = await response.Content.ReadAsStringAsync();
				var jsonDoc = JToken.Parse(responseBody);
				JArray modelArray = null;
				if (jsonDoc is JArray arr)
					modelArray = arr;
				else if (jsonDoc is JObject obj && obj.ContainsKey("data") && obj["data"] is JArray dataArr)
					modelArray = dataArr;
				else {
					result.ErrorMessage = "Invalid response format: missing 'data' field";
					return result;
				}

				var models = new List<IAIModelProvider.SimpleModel>();

				foreach (var modelElement in modelArray) {
					try {
						var id = modelElement["id"]?.ToString();
						if (string.IsNullOrWhiteSpace(id))
							continue;

						var createdToken = modelElement["created"];
						long? created = (createdToken != null && createdToken.Type == JTokenType.Integer)
							? createdToken.Value<long>()
							: null;

						var ownedBy = modelElement["owned_by"]?.ToString();
						
						var displayName = (modelElement["display_name"] ?? modelElement["name"])?.ToString();
						if (String.IsNullOrWhiteSpace(displayName))
							displayName = id;

						var tooltip = $"ID: {id}";
						if (!string.IsNullOrWhiteSpace(ownedBy))
							tooltip += $"\nOwned by: {ownedBy}";
						if (created.HasValue)
							tooltip += $"\nCreated: {DateTimeOffset.FromUnixTimeSeconds(created.Value):yyyy-MM-dd}";

						var summary = modelElement["summary"]?.ToString();
						if (! string.IsNullOrWhiteSpace(summary))
							tooltip += $"\n{summary}";

						models.Add(new IAIModelProvider.SimpleModel {
							Id = id,
							DisplayName = displayName,
							Tooltip = tooltip,
							Created = created
						});
					} catch {
						// Skip models that fail to parse
						continue;
					}
				}

				// Sort: common models first (in order), then remaining by created date descending
				//var commonModelsList = models.Where(m => CommonModels.Contains(m.Id)).ToList();
				//var sortedCommonModels = CommonModels
				//	.Where(commonId => commonModelsList.Any(m => m.Id == commonId))
				//	.Select(commonId => commonModelsList.First(m => m.Id == commonId))
				//	.ToList();

				var remainingModels = models
					//.Where(m => !CommonModels.Contains(m.Id))
					.OrderByDescending(m => m.Created ?? 0)
					.ToList();

				//result.Models.AddRange(sortedCommonModels);
				result.Models.AddRange(remainingModels);
				result.Success = true;

			} catch (TaskCanceledException) {
				result.ErrorMessage = "Request timed out. Please check your internet connection and endpoint URL.";
			} catch (HttpRequestException ex) {
				result.ErrorMessage = $"Network error: {ex.Message}";
			} catch (JsonException ex) {
				result.ErrorMessage = $"Failed to parse response: {ex.Message}";
			} catch (Exception ex) {
				result.ErrorMessage = $"Unexpected error: {ex.Message}";
			}

			return result;
		}
	}
}
