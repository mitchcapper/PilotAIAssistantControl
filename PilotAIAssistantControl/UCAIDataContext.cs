using PilotAIAssistantControl.MVVM;
using System.Collections.ObjectModel;
#if WPF
using System.Windows.Controls;
#else
using Microsoft.UI.Xaml.Controls;
#endif

namespace PilotAIAssistantControl {
	public class UCAIDataContext : BaseNotifyObject {
		public bool ShowTokenConfig => SelectedPendingProvider?.CustomConfigControl == null;
		public ObservableCollection<ChatItem> Messages { get; set => Set(ref field, value); } = new();
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
#if WPF
		public TabItem ActiveTab {
#else
		public TabViewItem ActiveTab {
#endif
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
}
