using System;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace PilotAIAssistantControl {
	/// <summary>
	/// Interaction logic for UCConfigureCopilot.xaml
	/// Handles GitHub Copilot-specific authentication and model discovery.
	/// Works with GithubCopilotProvider via DataContext.
	/// </summary>
	public partial class UCConfigureCopilot : UserControl {
		private CancellationTokenSource? _signInCancellation;
		private CopilotTokenHelper.CopilotApiToken? _cachedApiToken = null;

		/// <summary>
		/// The provider being configured (accessed via DataContext).
		/// </summary>
		public GithubCopilotProvider? Provider => DataContext as GithubCopilotProvider;

		

		/// <summary>
		/// Gets the cached API token from a successful token exchange.
		/// </summary>
		public CopilotTokenHelper.CopilotApiToken? CachedApiToken => _cachedApiToken;

		public UCConfigureCopilot() {
			InitializeComponent();
			PopulateTokenLocations();

		}

		private void PopulateTokenLocations() {
			var locations = CopilotTokenHelper.GetPossibleTokenLocations();
			var sb = new StringBuilder();
			sb.AppendLine("The token is searched in these locations:");
			foreach (var loc in locations) {
				var exists = System.IO.File.Exists(loc);
				sb.AppendLine($"• {(exists ? "✓" : "")}{loc}");
			}
			TxtTokenLocations.Text = sb.ToString();
		}

		private void WhereToken_Collapsed(object sender, RoutedEventArgs e) => e.Handled = true;

	


		

		

		#region Sign-In Flow

		private async void SignInWithGitHub_Click(object sender, RoutedEventArgs e) {
			_signInCancellation?.Cancel();
			_signInCancellation = new CancellationTokenSource();

			BtnSignIn.IsEnabled = false;
			DeviceFlowPanel.Visibility = Visibility.Visible;
			TxtDeviceCode.Text = "...";
			TxtDeviceUrl.Text = "Initiating...";

			try {
				var result = await CopilotTokenHelper.AcquireTokenViaDeviceFlowAsync(
					progressCallback: (userCode, verificationUri) => {
						Dispatcher.Invoke(() => {
							TxtDeviceCode.Text = userCode;
							TxtDeviceUrl.Text = verificationUri;
						});
					},
					cancellationToken: _signInCancellation.Token
				);

				if (result.Success && !string.IsNullOrEmpty(result.Token)) {
					if (Provider != null)
						Provider.UserData.Token = result.Token;
					DeviceFlowPanel.Visibility = Visibility.Collapsed;
					OnStatusMessage("✓ Signed in successfully! Loading models...", isError: false);

					await Provider.LoadModelsFromApi();
				} else {
					OnStatusMessage(result.ErrorMessage ?? "Sign-in failed", isError: true);
				}
			} catch (OperationCanceledException) {
				OnStatusMessage("Sign-in was cancelled", isError: false);
			} catch (Exception ex) {
				OnStatusMessage($"Sign-in error: {ex.Message}", isError: true);
			} finally {
				BtnSignIn.IsEnabled = true;
				DeviceFlowPanel.Visibility = Visibility.Collapsed;
			}
		}

		private void CancelSignIn_Click(object sender, RoutedEventArgs e) {
			_signInCancellation?.Cancel();
			DeviceFlowPanel.Visibility = Visibility.Collapsed;
			BtnSignIn.IsEnabled = true;
		}

		private void CopyDeviceCode_Click(object sender, RoutedEventArgs e) {
			try {
				Clipboard.SetText(TxtDeviceCode.Text);
				OnStatusMessage("Code copied to clipboard!", isError: false);
			} catch {
				// Clipboard access can fail
			}
		}

		#endregion

		#region Event Helpers
		private void OnStatusMessage(string message, bool isError) {
			Provider?.RaiseStatusMessage(message, isError);
		}

		#endregion

		private void ChkAutoDiscover_Checked(object sender, RoutedEventArgs e) {
			if (! IsLoaded)
				return;
			if (ChkAutoDiscover.IsChecked == true)
				Provider?.ProviderSelected(); // autodiscover loadl models 
		}
	}
}
