using System;
using System.IO;
using System.Text;
#if WPF
using System.Windows;
using System.Windows.Controls;
#else
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
#endif
using Newtonsoft.Json;
using PilotAIAssistantControl;
using TestWPFApp;
#if WPF
namespace TestWPFApp {
#else
namespace TestWinUIApp {
#endif
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window {
		public static MainWindow? Instance;
		private bool _isLoaded;
		public MainWindow() {
			Instance = this;
			InitializeComponent();

#if WPF
			Loaded += MainWindow_Loaded;
			Closing += MainWindow_Closing;
#else
			Activated += Window_Activated;
			Closed += MainWindow_Closed;
#endif
		}

#if WPF
		private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e) {
			SaveConfig();
		}
#else
		private void MainWindow_Closed(object sender, WindowEventArgs args) {
			SaveConfig();
		}
		public TextBox txtTestPublic => txtTest;
		public TextBox txtRegexPublic => txtRegex;
		//public 
#endif

		private void SaveConfig() {
			var data = ucAi.AiControl.ExportData();
			File.WriteAllText(json_config, JsonConvert.SerializeObject(data));
		}
#if WPF
		private static string json_config = @"test_exported_data.json";
#else
		private static string json_config = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), @"test_exported_data.json");
#endif
		private OurOptions GeneratorAgentOpts;
		private SimpleExplainerOptions QueryAgentOpts;
#if WPF
		private void MainWindow_Loaded(object sender, RoutedEventArgs e) {
#else
		private void Window_Activated(object sender, WindowActivatedEventArgs args) {
#endif
			_isLoaded = true;
			this.GeneratorAgentOpts = new OurOptions();
			this.QueryAgentOpts = new SimpleExplainerOptions();
			ucAi.AiControl.Configure(GeneratorAgentOpts);
			if (File.Exists(json_config)) {
				var data = JsonConvert.DeserializeObject<AIUserConfig>(File.ReadAllText(json_config));
				ucAi.AiControl.ImportData(data);
			}
			var pos = Array.IndexOf(Environment.GetCommandLineArgs(),"--load");
			if (pos > -1) 
				txtTest.Text = File.ReadAllText(Environment.GetCommandLineArgs()[pos+1]);
			ucAi.ExpanderControl.IsExpanded = true;
		}

		private void agentCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) {
			if (!_isLoaded)
				return;
			AIOptions newAgent = agentCombo.SelectedItem?.ToString()?.Contains("Explain") == true ? QueryAgentOpts : GeneratorAgentOpts;
			ucAi.Configure(newAgent);
		}

		private async void ExplainRegex_Click(object sender, RoutedEventArgs e) {
			agentCombo.SelectedIndex = 1; // Switch to Explain agent
			await ucAi.SendMessage($"`{txtRegex.Text}`");
		}


	}
}
