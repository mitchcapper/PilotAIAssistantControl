using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PilotAIAssistantControl {
	/// <summary>
	/// Helper class to discover GitHub Copilot OAuth tokens and available models.
	/// Uses the two-step authentication flow:
	/// 1. OAuth token (from device flow or discovered from config) - long-lived
	/// 2. API token exchange (OAuth token → short-lived API token + dynamic endpoint)
	/// Based on neowin copilot_chat.rs implementation.
	/// </summary>
	public static class CopilotTokenHelper {
		// Token exchange endpoint - returns API token and dynamic API endpoint
		private const string GitHubTokenExchangeUrl = "https://api.github.com/copilot_internal/v2/token";

		// GitHub OAuth Device Flow endpoints
		private const string GitHubDeviceCodeUrl = "https://github.com/login/device/code";
		private const string GitHubTokenUrl = "https://github.com/login/oauth/access_token";
		// Copilot's client ID (used by VS Code Copilot extension)
		private const string CopilotClientId = "Iv1.b507a08c87ecfe98";
		/// <summary>
		/// Represents a model available through the Copilot API.
		/// </summary>
		public class CopilotModel : IAIModelProvider.IModel {
			public string Id { get; set; } = string.Empty;
			public string Name { get; set; } = string.Empty;
			public string? Description { get; set; }
			public bool IsPreview { get; set; }
			public bool IsBeta { get; set; }
			public double? TokenMultiplier { get; set; }
			public string? Vendor { get; set; }
			public string? Family { get; set; }
			public int? MaxInputTokens { get; set; }
			public int? MaxOutputTokens { get; set; }

			// IModel implementation
			public string DisplayName => GetDisplayName();
			public string Tooltip => BuildTooltip();

			private string BuildTooltip() {
				var sb = new StringBuilder();
				sb.AppendLine($"ID: {Id}");

				if (!string.IsNullOrEmpty(Vendor))
					sb.AppendLine($"Vendor: {Vendor}");

				if (!string.IsNullOrEmpty(Family))
					sb.AppendLine($"Family: {Family}");

				if (TokenMultiplier.HasValue)
					sb.AppendLine($"Token Rate: {TokenMultiplier.Value}x");

				if (MaxInputTokens.HasValue)
					sb.AppendLine($"Max Input: {MaxInputTokens.Value:N0} tokens");

				if (MaxOutputTokens.HasValue)
					sb.AppendLine($"Max Output: {MaxOutputTokens.Value:N0} tokens");

				if (IsBeta)
					sb.AppendLine("⚠️ Beta - may be unstable");
				else if (IsPreview)
					sb.AppendLine("⚠️ Preview - subject to change");

				if (!string.IsNullOrEmpty(Description)) {
					sb.AppendLine();
					sb.AppendLine(Description);
				}

				return sb.ToString().TrimEnd();
			}

			public string GetDisplayName() {
				var sb = new StringBuilder(Name);

				var tags = new List<string>();

				// Show token multiplier first (this is the "cost")
				if (TokenMultiplier.HasValue) {
					if (TokenMultiplier.Value == 0)
						tags.Add("free");
					else if (TokenMultiplier.Value == 1.0)
						tags.Add("1x");
					else
						tags.Add($"{TokenMultiplier.Value}x");
				}

				if (IsBeta) tags.Add("Beta");
				else if (IsPreview) tags.Add("Preview");

				if (tags.Count > 0) {
					sb.Append(" [");
					sb.Append(string.Join(", ", tags));
					sb.Append("]");
				}

				return sb.ToString();
			}

			public override string ToString() => GetDisplayName();
		}

		/// <summary>
		/// Represents the short-lived API token obtained from OAuth token exchange.
		/// This token includes the actual API endpoint to use for requests.
		/// </summary>
		public class CopilotApiToken {
			/// <summary>
			/// The API token to use in Authorization: Bearer header.
			/// </summary>
			public string Token { get; set; } = string.Empty;

			/// <summary>
			/// Unix timestamp when the token expires.
			/// </summary>
			public long ExpiresAt { get; set; }

			/// <summary>
			/// The API endpoint to use for requests (from endpoints.api in response).
			/// This is the base URL for /models, /chat/completions, etc.
			/// </summary>
			public string ApiEndpoint { get; set; } = string.Empty;

			/// <summary>
			/// Returns whether the token has expired or is about to expire.
			/// </summary>
			public bool IsExpired => DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= ExpiresAt - 60; // 60 second buffer

			/// <summary>
			/// Returns the remaining seconds until expiration.
			/// </summary>
			public long RemainingSeconds => Math.Max(0, ExpiresAt - DateTimeOffset.UtcNow.ToUnixTimeSeconds());
		}

		/// <summary>
		/// Result of the device flow authentication.
		/// </summary>
		public class DeviceFlowResult {
			public bool Success { get; set; }
			public string? Token { get; set; }
			public string? ErrorMessage { get; set; }
			public string? UserCode { get; set; }
			public string? VerificationUri { get; set; }
		}

		/// <summary>
		/// Callback for device flow progress updates.
		/// </summary>
		public delegate void DeviceFlowProgressCallback(string userCode, string verificationUri);

		private static HttpClient GetClient(IWebProxy? proxy = default) {
			if (proxy == null) {
				return new HttpClient();
			} else {
				var handler = new HttpClientHandler {
					Proxy = proxy,
					UseProxy = true,
				};
				return new HttpClient(handler);
			}
		}
		/// <summary>
		/// Initiates GitHub OAuth Device Flow to acquire a Copilot token.
		/// This opens a browser for the user to authenticate.
		/// </summary>
		/// <param name="progressCallback">Called with the user code and verification URI for display.</param>
		/// <param name="cancellationToken">Token to cancel the operation.</param>
		/// <returns>Result containing the token or error information.</returns>
		public static async Task<DeviceFlowResult> AcquireTokenViaDeviceFlowAsync(
			DeviceFlowProgressCallback? progressCallback = null,
			CancellationToken cancellationToken = default, IWebProxy? proxy = default) {
			var result = new DeviceFlowResult();

			using var client = GetClient(proxy);
			client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

			try {
				// Step 1: Request device code
				var deviceCodeRequest = new FormUrlEncodedContent(new[]
				{
					new KeyValuePair<string, string>( "client_id", CopilotClientId ),
					new KeyValuePair<string, string>( "scope", "read:user" )
				});

				var deviceCodeResponse = await client.PostAsync(GitHubDeviceCodeUrl, deviceCodeRequest, cancellationToken);
				var deviceCodeJson = await deviceCodeResponse.Content.ReadAsStringAsync(cancellationToken);

				if (!deviceCodeResponse.IsSuccessStatusCode) {
					result.Success = false;
					result.ErrorMessage = $"Failed to initiate device flow: {deviceCodeResponse.StatusCode}";
					return result;
				}

				var deviceDoc = JObject.Parse(deviceCodeJson);
				var deviceCode = deviceDoc["device_code"]?.ToString();
				var userCode = deviceDoc["user_code"]?.ToString();
				var verificationUri = deviceDoc["verification_uri"]?.ToString();
				var expiresIn = deviceDoc["expires_in"]?.Value<int>() ?? 900;
				var interval = deviceDoc["interval"]?.Value<int>() ?? 5;

				result.UserCode = userCode;
				result.VerificationUri = verificationUri;

				// Notify caller of the code to display
				progressCallback?.Invoke(userCode!, verificationUri!);

				// Open browser for user to authenticate
				try {
					Process.Start(new ProcessStartInfo {
						FileName = verificationUri,
						UseShellExecute = true
					});
				} catch {
					// Browser failed to open, user will need to navigate manually
				}

				// Step 2: Poll for token
				var pollUntil = DateTime.UtcNow.AddSeconds(expiresIn);

				while (DateTime.UtcNow < pollUntil) {
					cancellationToken.ThrowIfCancellationRequested();

					await Task.Delay(TimeSpan.FromSeconds(interval), cancellationToken);

					var tokenRequest = new FormUrlEncodedContent(new[]
					{
						new KeyValuePair<string, string>( "client_id", CopilotClientId ),
						new KeyValuePair<string, string>( "device_code", deviceCode! ),
						new KeyValuePair<string, string>( "grant_type", "urn:ietf:params:oauth:grant-type:device_code" )
					});

					var tokenResponse = await client.PostAsync(GitHubTokenUrl, tokenRequest, cancellationToken);
					var tokenJson = await tokenResponse.Content.ReadAsStringAsync(cancellationToken);

					var tokenDoc = JObject.Parse(tokenJson);

					if (tokenDoc.ContainsKey("access_token")) {
						result.Success = true;
						result.Token = tokenDoc["access_token"]?.ToString();

						// Save the token for future use
						await SaveTokenAsync(result.Token!);

						return result;
					}

					if (tokenDoc.ContainsKey("error")) {
						var errorCode = tokenDoc["error"]?.ToString();

						if (errorCode == "authorization_pending") {
							// User hasn't completed auth yet, keep polling
							continue;
						} else if (errorCode == "slow_down") {
							interval += 5;
							continue;
						} else if (errorCode == "expired_token") {
							result.Success = false;
							result.ErrorMessage = "Authentication timed out. Please try again.";
							return result;
						} else if (errorCode == "access_denied") {
							result.Success = false;
							result.ErrorMessage = "Authentication was denied by the user.";
							return result;
						} else {
							result.Success = false;
							result.ErrorMessage = $"Authentication error: {errorCode}";
							return result;
						}
					}
				}

				result.Success = false;
				result.ErrorMessage = "Authentication timed out. Please try again.";
			} catch (OperationCanceledException) {
				result.Success = false;
				result.ErrorMessage = "Authentication was cancelled.";
			} catch (Exception ex) {
				result.Success = false;
				result.ErrorMessage = $"Authentication failed: {ex.Message}";
			}

			return result;
		}

		/// <summary>
		/// Saves the token to the standard Copilot config location.
		/// </summary>
		private static async Task SaveTokenAsync(string token) {
			try {
				var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
				var configDir = Path.Combine(localAppData, "github-copilot");
				var appsJsonPath = Path.Combine(configDir, "apps.json");

				Directory.CreateDirectory(configDir);

				var config = new Dictionary<string, object> {
					["regexpress"] = new Dictionary<string, string> {
						["oauth_token"] = token
					}
				};

				// If file exists, try to merge
				if (File.Exists(appsJsonPath)) {
					try {
						var existingJson = await File.ReadAllTextAsync(appsJsonPath);
						var doc = JObject.Parse(existingJson);
						foreach (var prop in doc.Properties()) {
							if (prop.Name != "regexpress") {
								config[prop.Name] = prop.Value.ToObject<object>()!;
							}
						}
					} catch {
						// Ignore merge errors, just overwrite
					}
				}

				var json = JsonConvert.SerializeObject(config, Formatting.Indented);
				await File.WriteAllTextAsync(appsJsonPath, json);
			} catch {
				// Silently fail - token is still usable for this session
			}
		}

		/// <summary>
		/// Attempts to discover the Copilot OAuth token from known config locations.
		/// Checks:
		/// 1. %LOCALAPPDATA%\github-copilot\apps.json (JetBrains IDEs)
		/// 2. %APPDATA%\github-copilot\hosts.json (older format)
		/// </summary>
		/// <returns>The OAuth token if found, null otherwise.</returns>
		public static string? DiscoverToken() {
			// Primary location: JetBrains IDEs
			var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			var appsJsonPath = Path.Combine(localAppData, "github-copilot", "apps.json");

			if (File.Exists(appsJsonPath)) {
				var token = ExtractTokenFromAppsJson(appsJsonPath);
				if (!string.IsNullOrEmpty(token))
					return token;
			}

			// Alternative location: older Neovim/VSCode format
			var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
			var hostsJsonPath = Path.Combine(appData, "github-copilot", "hosts.json");

			if (File.Exists(hostsJsonPath)) {
				var token = ExtractTokenFromHostsJson(hostsJsonPath);
				if (!string.IsNullOrEmpty(token))
					return token;
			}

			// Check home directory config (cross-platform pattern)
			var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
			var configAppsPath = Path.Combine(homeDir, ".config", "github-copilot", "apps.json");

			if (File.Exists(configAppsPath)) {
				var token = ExtractTokenFromAppsJson(configAppsPath);
				if (!string.IsNullOrEmpty(token))
					return token;
			}

			return null;
		}

		/// <summary>
		/// Gets a list of possible token file locations for user reference.
		/// </summary>
		public static List<string> GetPossibleTokenLocations() {
			var locations = new List<string>();

			var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			locations.Add(Path.Combine(localAppData, "github-copilot", "apps.json"));

			var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
			locations.Add(Path.Combine(appData, "github-copilot", "hosts.json"));

			var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
			locations.Add(Path.Combine(homeDir, ".config", "github-copilot", "apps.json"));

			return locations;
		}

		private static string? ExtractTokenFromAppsJson(string path) {
			try {
				var json = File.ReadAllText(path);
				var doc = JObject.Parse(json);

				// The apps.json format is typically an object with app entries
				// Each entry may have an oauth_token field
				foreach (var property in doc.Properties()) {
					if (property.Value is JObject obj && obj.ContainsKey("oauth_token")) {
						var token = obj["oauth_token"]?.ToString();
						if (!string.IsNullOrEmpty(token))
							return token;
					}
				}
			} catch {
				// Ignore parsing errors
			}
			return null;
		}

		private static string? ExtractTokenFromHostsJson(string path) {
			try {
				var json = File.ReadAllText(path);
				var doc = JObject.Parse(json);

				// The hosts.json format from older plugins
				if (doc.ContainsKey("github.com")) {
					var githubEntry = doc["github.com"];
					if (githubEntry is JObject githubObj && githubObj.ContainsKey("oauth_token")) {
						return githubObj["oauth_token"]?.ToString();
					}
				}
			} catch {
				// Ignore parsing errors
			}
			return null;
		}

		/// <summary>
		/// Result of fetching models from the API.
		/// </summary>
		public class FetchModelsResult {
			public List<CopilotModel> Models { get; set; } = new();
			public bool Success { get; set; }
			public string? ErrorMessage { get; set; }

		}

		/// <summary>
		/// Result of exchanging an OAuth token for an API token.
		/// </summary>
		public class ApiTokenExchangeResult {
			public bool Success { get; set; }
			public CopilotApiToken? ApiToken { get; set; }
			public string? ErrorMessage { get; set; }
		}

		/// <summary>
		/// Exchanges an OAuth token for a short-lived API token.
		/// The API token includes the actual endpoint to use for API requests.
		/// </summary>
		/// <param name="oauthToken">The OAuth token from device flow or discovered from config.</param>
		/// <param name="proxy">Optional proxy for HTTP requests.</param>
		/// <param name="enterpriseUri">Optional GitHub Enterprise URI (e.g., https://github.mycompany.com).</param>
		/// <returns>Result containing the API token or error information.</returns>
		public static async Task<ApiTokenExchangeResult> ExchangeOAuthForApiTokenAsync(
			string oauthToken,
			IWebProxy? proxy = default,
			string? enterpriseUri = null) {
			var result = new ApiTokenExchangeResult();

			if (string.IsNullOrEmpty(oauthToken)) {
				result.Success = false;
				result.ErrorMessage = "No OAuth token provided.";
				return result;
			}

			// Determine the token exchange URL based on whether this is enterprise or not
			var tokenUrl = GetTokenExchangeUrl(enterpriseUri);

			using var client = GetClient(proxy);
			client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

			try {
				// Create request with OAuth token in header
				// Note: OAuth token uses "token" prefix, not "Bearer"
				using var request = new HttpRequestMessage(HttpMethod.Get, tokenUrl);
				request.Headers.Authorization = new AuthenticationHeaderValue("token", oauthToken);
				foreach (var header in HeadersToAdd)
					request.Headers.TryAddWithoutValidation(header.Key, header.Value);

				var response = await client.SendAsync(request);

				if (!response.IsSuccessStatusCode) {
					var errorBody = await response.Content.ReadAsStringAsync();
					result.Success = false;
					result.ErrorMessage = response.StatusCode switch {
						HttpStatusCode.Unauthorized => "OAuth token is invalid or expired. Please re-authenticate.",
						HttpStatusCode.Forbidden => "Access denied. Your GitHub account may not have Copilot access.",
						_ => $"Token exchange failed ({response.StatusCode}): {errorBody}"
					};
					return result;
				}

				var json = await response.Content.ReadAsStringAsync();
				var doc = JObject.Parse(json);

				var apiToken = new CopilotApiToken();

				if (doc.ContainsKey("token"))
					apiToken.Token = doc["token"]?.ToString() ?? string.Empty;

				if (doc.ContainsKey("expires_at"))
					apiToken.ExpiresAt = doc["expires_at"]?.Value<long>() ?? 0;

				if (doc.ContainsKey("endpoints")) {
					var endpointsElement = doc["endpoints"];
					if (endpointsElement is JObject endpointsObj && endpointsObj.ContainsKey("api"))
						apiToken.ApiEndpoint = endpointsObj["api"]?.ToString() ?? string.Empty;
				}

				if (string.IsNullOrEmpty(apiToken.Token) || string.IsNullOrEmpty(apiToken.ApiEndpoint)) {
					result.Success = false;
					result.ErrorMessage = "Token exchange response missing required fields.";
					return result;
				}

				result.Success = true;
				result.ApiToken = apiToken;
			} catch (Exception ex) {
				result.Success = false;
				result.ErrorMessage = $"Token exchange failed: {ex.Message}";
			}

			return result;
		}

		/// <summary>
		/// Gets the token exchange URL, supporting GitHub Enterprise.
		/// </summary>
		private static string GetTokenExchangeUrl(string? enterpriseUri) {
			if (string.IsNullOrEmpty(enterpriseUri))
				return GitHubTokenExchangeUrl;

			var domain = ParseDomain(enterpriseUri);
			return $"https://api.{domain}/copilot_internal/v2/token";
		}

		/// <summary>
		/// Parses the domain from an enterprise URI.
		/// </summary>
		private static string ParseDomain(string uri) {
			uri = uri.TrimEnd('/');

			if (uri.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
				uri = uri.Substring(8);
			else if (uri.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
				uri = uri.Substring(7);

			var slashIndex = uri.IndexOf('/');
			return slashIndex >= 0 ? uri.Substring(0, slashIndex) : uri;
		}
		internal static Task<ApiTokenExchangeResult> EnsureValidSessionToken(string oauthToken, CopilotApiToken? CurSessionToken, string? enterpriseUri = null, IWebProxy? proxy = default) {
			var cur = CurSessionValidateTask;
			if (cur != null)
				return cur;
			try {
				var task = _EnsureValidSessionToken(oauthToken, CurSessionToken, enterpriseUri, proxy);
				CurSessionValidateTask = task;
				return task;
			} finally {
				CurSessionValidateTask = null;
			}
		}
		private static Task<ApiTokenExchangeResult>? CurSessionValidateTask;
		private static async Task<ApiTokenExchangeResult> _EnsureValidSessionToken(string oauthToken, CopilotApiToken? CurSessionToken, string? enterpriseUri = null, IWebProxy? proxy = default) {
			if (string.IsNullOrEmpty(oauthToken)) {
				return new() { ErrorMessage = "No OAuth token provided. Please auto-detect or enter your Copilot token." };

			}
			if (CurSessionToken?.IsExpired == false)
				return new ApiTokenExchangeResult {
					Success = true,
					ApiToken = CurSessionToken
				};

			return await ExchangeOAuthForApiTokenAsync(oauthToken, proxy, enterpriseUri);

		}
		internal static KeyValuePair<string, string>[] HeadersToAdd = [
				new("User-Agent", "GitHubCopilotChat/0.24.2025012401"),
				new("Copilot-Integration-Id", "vscode-chat"),
				new("Editor-Version", "vscode/1.103.2"),
				new("x-github-api-version", "2025-05-01")
			];
		/// <summary>
		/// Fetches available models from the GitHub Copilot API.
		/// Uses the two-step auth flow: OAuth token → API token → models request.
		/// </summary>
		/// <param name="oauthToken">The OAuth token to use for authentication.</param>
		/// <param name="proxy">Optional proxy for HTTP requests.</param>
		/// <param name="cachedApiToken">Optional cached API token to reuse if not expired.</param>
		/// <param name="enterpriseUri">Optional GitHub Enterprise URI.</param>
		/// <returns>A result containing available models or error information.</returns>
		public static async Task<FetchModelsResult> FetchAvailableModelsAsync(CopilotApiToken sessionToken, IWebProxy? proxy = default) {
			var result = new FetchModelsResult();

			// Step 2: Fetch models using the API token and endpoint from the token response
			var modelsUrl = $"{sessionToken.ApiEndpoint}/models";

			using var client = GetClient(proxy);
			client.Timeout = TimeSpan.FromSeconds(30);

			try {
				using var request = new HttpRequestMessage(HttpMethod.Get, modelsUrl);
				// API token uses "Bearer" prefix
				request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sessionToken.Token);
				foreach (var header in HeadersToAdd)
					request.Headers.TryAddWithoutValidation(header.Key, header.Value);


				var response = await client.SendAsync(request);

				if (!response.IsSuccessStatusCode) {
					result.Success = false;
					result.ErrorMessage = response.StatusCode switch {
						HttpStatusCode.Unauthorized => "API token is invalid or expired. Please re-authenticate with Copilot.",
						HttpStatusCode.Forbidden => "Access denied. Your Copilot subscription may not have access to this API.",
						_ => $"API error: {response.StatusCode}"
					};
					return result;
				}

				var json = await response.Content.ReadAsStringAsync();
				var doc = JObject.Parse(json);

				if (doc.ContainsKey("data") && doc["data"] is JArray dataArray) {
					foreach (var modelElement in dataArray) {
						try {
							var model = new CopilotModel();

							model.Id = modelElement["id"]?.ToString() ?? string.Empty;
							model.Name = modelElement["name"]?.ToString() ?? model.Id;

							// Skip models without an ID
							if (string.IsNullOrEmpty(model.Id))
								continue;

							// Check policy state - skip disabled models
							if (modelElement["policy"] is JObject policyElement) {
								if (policyElement["state"] is JToken stateElement) {
									var state = stateElement.ToString();
									if (state != null && state != "enabled")
										continue; // Skip disabled models
								}
							}

							// Check model_picker_enabled - only show models that can be selected
							if (modelElement["model_picker_enabled"] is JToken pickerElement) {
								if (pickerElement.Type == JTokenType.Boolean && !pickerElement.Value<bool>())
									continue; // Skip models not available in picker
							}

							// Parse billing info (from billing.multiplier and billing.is_premium)
							if (modelElement["billing"] is JObject billingElement) {
								if (billingElement["multiplier"] is JToken multiplierElement) {
									if (multiplierElement.Type == JTokenType.Integer || multiplierElement.Type == JTokenType.Float)
										model.TokenMultiplier = multiplierElement.Value<double>();
								}

								if (billingElement["is_premium"] is JToken premiumElement)
									model.IsPreview = premiumElement.Value<bool>(); // Premium models shown as preview
							}

							// Parse capabilities info
							if (modelElement["capabilities"] is JObject capabilitiesElement) {
								if (capabilitiesElement["family"] is JToken familyElement)
									model.Family = familyElement.ToString();

								// Parse nested limits
								if (capabilitiesElement["limits"] is JObject limitsElement) {
									if (limitsElement["max_prompt_tokens"] is JToken promptTokens &&
										(promptTokens.Type == JTokenType.Integer || promptTokens.Type == JTokenType.Float))
										model.MaxInputTokens = (int)promptTokens.Value<long>();

									if (limitsElement["max_output_tokens"] is JToken outputTokens &&
										(outputTokens.Type == JTokenType.Integer || outputTokens.Type == JTokenType.Float))
										model.MaxOutputTokens = outputTokens.Value<int>();
								}
							}

							// Parse vendor info (can be string or object)
							if (modelElement["vendor"] is JToken vendorElement) {
								if (vendorElement.Type == JTokenType.String)
									model.Vendor = vendorElement.ToString();
							}

							// Check for is_chat_default flag
							if (modelElement["is_chat_default"] is JToken defaultElement) {
								if (defaultElement.Value<bool>())
									model.IsPreview = false; // Default models are not preview
							}
							if (model.Name.Contains("beta", StringComparison.CurrentCultureIgnoreCase))
								model.IsBeta = true;
							string[] replace_strs = ["(preview)", "(beta)", "preview", "beta"];
							foreach (var str in replace_strs)
								model.Name = model.Name.Replace(str, "", StringComparison.OrdinalIgnoreCase);
							model.Name = model.Name.Trim().Replace("  ", " ");

							result.Models.Add(model);
						} catch {
							// Skip models that fail to parse (resilient parsing like Rust reference)
						}
					}
				}

				// Sort models: non-preview/beta first, then by token multiplier (lower first), then alphabetically
				result.Models = result.Models
					.OrderBy(m => m.IsBeta || m.IsPreview ? 1 : 0)
					.ThenBy(m => m.TokenMultiplier ?? 1.0)
					.ThenBy(m => m.Name)
					.ToList();

				result.Success = true;
			} catch (TaskCanceledException) {
				result.Success = false;
				result.ErrorMessage = "Request timed out. Please check your internet connection.";
			} catch (HttpRequestException ex) {
				result.Success = false;
				result.ErrorMessage = $"Network error: {ex.Message}";
			} catch (JsonException) {
				result.Success = false;
				result.ErrorMessage = "Failed to parse API response.";
			} catch (Exception ex) {
				result.Success = false;
				result.ErrorMessage = $"Unexpected error: {ex.Message}";
			}

			return result;
		}

		/// <summary>
		/// Gets the chat completions URL for a given API token.
		/// </summary>
		public static string GetChatCompletionsUrl(CopilotApiToken apiToken) {
			return $"{apiToken.ApiEndpoint}/chat/completions";
		}

		/// <summary>
		/// Gets the responses URL for a given API token (alternative API endpoint).
		/// </summary>
		public static string GetResponsesUrl(CopilotApiToken apiToken) {
			return $"{apiToken.ApiEndpoint}/responses";
		}

		/// <summary>
		/// Gets the models URL for a given API token.
		/// </summary>
		public static string GetModelsUrl(CopilotApiToken apiToken) {
			return $"{apiToken.ApiEndpoint}/models";
		}
	}
}
