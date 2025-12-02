using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Newtonsoft.Json;
using PilotAIAssistantControl;

namespace TestWPFApp {
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window {
		public static MainWindow? Instance;
		public MainWindow() {
			Instance = this;
			InitializeComponent();
			Loaded += MainWindow_Loaded;
			Closing += MainWindow_Closing;
		}

		private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e) {
			var data = ucAi.AiControl.ExportData();

			File.WriteAllText(json_config, JsonConvert.SerializeObject(data));
		}
		private static string json_config = @"test_exported_data.json";
		private OurOptions GeneratorAgentOpts;
		private SimpleExplainerOptions QueryAgentOpts;

		private void MainWindow_Loaded(object sender, RoutedEventArgs e) {
			this.GeneratorAgentOpts = new OurOptions();
			this.QueryAgentOpts = new SimpleExplainerOptions();
			ucAi.AiControl.Configure(GeneratorAgentOpts);
			if (File.Exists(json_config)) {
				var data = JsonConvert.DeserializeObject<AIUserConfig>(File.ReadAllText(json_config));
				if (data != null)
					ucAi.AiControl.ImportData(data);
			}

			txtTest.Text = File.ReadAllText(@"C:\temp\scratch\gnu_short.html");
			ucAi.ExpanderControl.IsExpanded = true;
		}

		private async void agentCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) {
			if (! IsLoaded)
				return;
			AIOptions newAgent = agentCombo.SelectedItem.ToString().Contains("Explain") ? QueryAgentOpts : GeneratorAgentOpts;
			ucAi.Configure(newAgent);
		}

		private async void ExplainRegex_Click(object sender, RoutedEventArgs e) {
			agentCombo.SelectedIndex = 1; // Switch to Explain agent
			await ucAi.SendMessage($"`{txtRegex.Text}`");
		}
	}
}
