# PilotAiAssistControlWPF

A WPF user control that provides an AI chat assistant interface with support for multiple AI providers (GitHub Copilot, OpenAI, and custom providers). Includes both a standalone chat control and an expandable sidebar version.  It has integrated support for discovery of local existing GitHub Copilot tokens (ie from vscode) and also integrates a login flow for users that don't already have it present.

You can optionally provide a "reference text/document" which can be a longer document the user is working on and allow the user to control how much(if any) of it to send along.

<!-- TOC ignore:true -->

## TOC
<!-- TOC -->

- [PilotAiAssistControlWPF](#pilotaiassistcontrolwpf)
	- [Installation](#installation)
	- [Quick Start](#quick-start)
		- [Add Namespace](#add-namespace)
		- [Create Your Options Class](#create-your-options-class)
		- [Embed the Control](#embed-the-control)
		- [Initialize in Code-Behind](#initialize-in-code-behind)
	- [Requirements](#requirements)
	- [Saving and Restoring User Settings](#saving-and-restoring-user-settings)
	- [AIOptions Configuration](#aioptions-configuration)
		- [Reference Text Replace Actions](#reference-text-replace-actions)
	- [Code Block Actions](#code-block-actions)
		- [Built-in Actions](#built-in-actions)
		- [Creating Custom Actions](#creating-custom-actions)
		- [ICodeBlock Interface](#icodeblock-interface)
		- [Conditional Visibility](#conditional-visibility)
		- [Implementing the CodeblockAction Interface](#implementing-the-codeblockaction-interface)
		- [Example: Full Options Class](#example-full-options-class)
	- [UCAIExpandable Properties](#ucaiexpandable-properties)
	- [Multiple Agents / System Prompts](#multiple-agents--system-prompts)
		- [Example: Agent Switcher](#example-agent-switcher)
	- [Developer / Technical Notes](#developer--technical-notes)

<!-- /TOC -->


## Installation

```
Install-Package PilotAiAssistControlWPF
```

## Quick Start

### Add Namespace

```xml
xmlns:pia="clr-namespace:PilotAIAssistantControl;assembly=PilotAIAssistantControl"
```

### Create Your Options Class

Create a class inheriting from `AIOptions` to configure the AI behavior:

```csharp
public class RegexAIOptions : AIOptions {
    public override string GetSystemPrompt() =>
        "You are a C# Regex expert assistant. " +
        "Provide regex patterns inside Markdown code blocks (```regex ... ```). " +
        "Explain how the pattern works briefly.";
}
```

### Embed the Control

**Option A: Expandable Panel (Recommended for sidebars)**

```xml
<Grid>
    <Grid.ColumnDefinitions>
        <ColumnDefinition x:Name="AiColumn" Width="Auto"/>
        <ColumnDefinition Width="5"/>
        <ColumnDefinition Width="*"/>
    </Grid.ColumnDefinitions>

    <pia:UCAIExpandable x:Name="ucAiExpandable"
                        TargetColumn="{Binding ElementName=AiColumn}"
                        DefaultSize="400"
                        MinExpandedSize="200"
                        CollapsedSize="40"/>

    <GridSplitter Grid.Column="1" HorizontalAlignment="Stretch"/>

    <!-- Your main content in Grid.Column="2" -->
</Grid>
```

**Option B: Direct Control (For fixed layouts)**

```xml
<pia:UCAI x:Name="ucAi" Width="400"/>
```

### Initialize in Code-Behind

**Important:** You must call `Configure()` followed by `ImportData()` for the control to fully initialize. Always call `ImportData()` even if you have no data to restore (pass `null`).

```csharp
private void Window_Loaded(object sender, RoutedEventArgs e) {
    var options = new RegexAIOptions();

    // For expandable version:
    ucAiExpandable.AiControl.Configure(options);
    ucAiExpandable.AiControl.ImportData(null); // Required even with no saved data

    // Or for direct control:
    ucAi.Configure(options);
    ucAi.ImportData(null);
}
```

## Requirements

- .NET 6.0+ or .NET Framework 4.7.2+
- WPF application
- Newtonsoft.Json (for settings serialization)

## Saving and Restoring User Settings

The control supports saving and restoring the user's AI provider configuration:

```csharp
// Save settings (e.g., on window closing)
private void Window_Closing(object sender, CancelEventArgs e) {
    AIUserConfig data = ucAiExpandable.AiControl.ExportData();
    string json = JsonConvert.SerializeObject(data);
    File.WriteAllText("ai_settings.json", json);
}

// Restore settings (on load)
private void Window_Loaded(object sender, RoutedEventArgs e) {
    var options = new RegexAIOptions();
    ucAiExpandable.AiControl.Configure(options);

    AIUserConfig? savedData = null;
    if (File.Exists("ai_settings.json")) {
        savedData = JsonConvert.DeserializeObject<AIUserConfig>(
            File.ReadAllText("ai_settings.json"));
    }
    ucAiExpandable.AiControl.ImportData(savedData);
}
```

## AIOptions Configuration

The `AIOptions` class controls how the AI chat behaves. Override properties and methods to customize:

| Member | Description |
|--------|-------------|
| `string GetSystemPrompt()` | **Required.** Returns the system prompt that defines the AI's behavior and context. |
| `IAIModelProvider[] Providers` | Array of available AI providers. Defaults to built-in providers (GitHub Copilot, OpenAI, etc.). |
| `REFERENCE_TEXT_REPLACE_ACTION ReplaceAction` | Controls how reference text updates are handled in chat history. Default: `ChangeOldToPlaceholder`. |
| `stirng GetCurrentReferenceText()` | Returns the current reference text to send with queries (e.g., document content). |
| `string ReferenceTextHeader` | Header shown to the AI for reference text blocks. Default: `"Reference Text"`. |
| `string ReferenceTextDisplayName` | User-facing name for reference text. Default: `"Reference Text"`. |
| `int DefaultMaxReferenceTextCharsToSend` | Max characters of reference text to include. Default: `5000`. |
| `bool AllowUserToSetMaxReferenceTextCharsToSend` | Show UI to let user adjust max chars. Default: `true`. |
| `String HintForUserInput` | Placeholder text for the chat input box. Default: `"Ask your question here..."`. |
| `String FormatUserQuestion(string)` | Transform user questions before sending to AI. |
| `IEnumerable<CodeblockAction> CodeblockActions` | Custom actions shown on code blocks in AI responses. |
| `void HandleDebugMessage(string)` | Receive debug messages from the AI service. |

It may be non-obvious but if you want to disable the "reference text" functionality you should set `ReplaceAction = ReferenceTextDisabled` this will disable it completely and hide those UI Components.  See below for more details on options.

### Reference Text Replace Actions

When using reference text that changes during the conversation we likely don't want to include all the past versions to minimize the context window.  The enum value here controls how we handle that while trying to minimize any confusion on the ai model's side as to what happened in the conversation.

| Action | Behavior |
|--------|----------|
| `ReferenceTextDisabled` | Disables reference text feature entirely. |
| `ChangeOldToPlaceholder` | Replaces old reference text with a placeholder note. **(Recommended)** |
| `LeaveOldInplace` | Keeps old versions in history (uses more tokens). |
| `UpdateInPlace` | Updates reference text where it first appeared. |
| `DeleteOld` | Removes old reference text completely. |

## Code Block Actions

When the AI responds with code blocks (markdown fenced code), you can add action buttons that conditionally appear below each block. This lets users quickly apply, copy, or process the AI's code suggestions.

### Built-in Actions

- `GenericCodeblockAction.ClipboardAction` - A pre-built "📋 Copy" sample action that copies the code to clipboard.

### Creating Custom Actions

YOu can create your own custom actions either implementing the ICodeblockAction interface directly or using the `GenericCodeblockAction` helper base class to create custom actions:

```csharp
new GenericCodeblockAction("📝 Use as Pattern", async (block) => {
    // block.Code contains the code block content
    // block.Language contains the language identifier (e.g., "regex", "csharp")
    _mainWindow.txtPattern.Text = block.Code;
    return true; // Return true on success, false on failure
}) {
    Tooltip = "Use this code block as the regex pattern",
    FeedbackOnAction = "✓ Applied!"  // Shown briefly after clicking
}
```

### ICodeBlock Interface

The `block` parameter passed to your action implements `ICodeBlock`:

| Property | Type | Description |
|----------|------|-------------|
| `Code` | `string` | The content of the code block (without the fences). |
| `Language` | `string` | The language identifier from the fence (e.g., `regex`, `csharp`, `json`). |

### Conditional Visibility

To show actions only for specific code block types, set `IsVisibleDel`.  It will get the entire ICodeBlock object to determine if it wants to offer its action:

```csharp
new GenericCodeblockAction("📝 Use as Pattern", async (block) => {
    _mainWindow.txtPattern.Text = block.Code;
    return true;
}) {
    Tooltip = "Apply this regex pattern",
    FeedbackOnAction = "✓ Applied!",
    IsVisibleDel = (block) => block.Language == "regex"  // Only show for regex blocks
}
```

### Implementing the CodeblockAction Interface

For more control, implement the `CodeblockAction` interface directly:

```csharp
public interface ICodeblockAction {
    Task<bool> DoAction(ICodeBlock block);  // Execute the action
    string DisplayName { get; }              // Button text (e.g., "📋 Copy")
    string Tooltip => DisplayName;           // Hover tooltip
    string FeedbackOnAction => "✓ Done!";    // Shown after action completes
    bool IsVisible(ICodeBlock block) => true; // Control visibility per block
}
```

### Example: Full Options Class

```csharp
public class RegexAIOptions : AIOptions {
    private readonly MainWindow _mainWindow;

    public RegexAIOptions(MainWindow mainWindow) {
        _mainWindow = mainWindow;
    }

    public override string GetSystemPrompt() =>
        "You are a C# Regex expert assistant. The user has questions about their regex patterns and target text. " +
        "Provide regex patterns inside Markdown code blocks (```regex ... ```). " +
        "Explain how the pattern works briefly. " +
        "If the language supports named capture groups, use these by default.";

    public override string GetCurrentReferenceText() =>
        _mainWindow.txtTargetText.Text;

    public override string FormatUserQuestion(string userQuestion) =>
        $"Current pattern:\n```regex\n{_mainWindow.txtPattern.Text}\n```\n\nMy question: {userQuestion}";

    public override string ReferenceTextHeader => "Users current target text";
    public override string ReferenceTextDisplayName => "Target Text";
    public override string HintForUserInput => "Ask about a pattern or matching...";
    public override int DefaultMaxReferenceTextCharsToSend => 5000;

    public override IEnumerable<CodeblockAction> CodeblockActions => [
        GenericCodeblockAction.ClipboardAction,
        new GenericCodeblockAction("📝 Use as Pattern", async (block) => {
            _mainWindow.txtPattern.Text = block.Code;
            return true;
        }) {
            Tooltip = "Use this code block as the regex pattern",
            FeedbackOnAction = "✓ Applied!"
        }
    ];

    public override void HandleDebugMessage(string msg) =>
        System.Diagnostics.Debug.WriteLine(msg);
}
```

## UCAIExpandable Properties

| Property | Type | Description |
|----------|------|-------------|
| `TargetColumn` | `ColumnDefinition` | Grid column to resize on expand/collapse (horizontal mode). |
| `TargetRow` | `RowDefinition` | Grid row to resize on expand/collapse (vertical mode). |
| `DefaultSize` | `double` | Initial expanded size. Default: `400`. |
| `MinExpandedSize` | `double` | Minimum size when expanded. Default: `200`. |
| `CollapsedSize` | `double` | Size when collapsed. Default: `40`. |
| `IsExpanded` | `bool` | Current expansion state (two-way bindable). |
| `Header` | `object` | Custom header content for the expander. |
| `AiControl` | `UCAI` | Access to the inner AI chat control. |
| `ExpanderControl` | `Expander` | Access to the inner Expander control. |

## Multiple Agents / System Prompts

The control doesn't have built-in UI for switching between multiple AI "agents" (different system prompts), but you can easily implement this yourself by calling `Configure()` with different `AIOptions` instances. If the "Providers" property of the AIOptions is the same between AiOption instances then the user's provider settings are preserved across agent switches.

### Example: Agent Switcher

Define multiple agents with different system prompts:

```csharp
// Agent 1: Generates regex patterns based on user requirements
public class RegexGeneratorAgent : AIOptions {
    public override string GetSystemPrompt() =>
        "You are a C# Regex expert. Help users create regex patterns. " +
        "Provide patterns in ```regex code blocks.";

    public override string HintForUserInput => "Describe what you want to match...";
    public override string ReferenceTextHeader => "Users current target text";
    public override string GetCurrentReferenceText() => MainWindow.Instance.txtTest.Text;

    public override string FormatUserQuestion(string userQuestion) =>
        $"Current pattern:\n```regex\n{MainWindow.Instance.txtRegex.Text}\n```\n\n{userQuestion}";
}

// Agent 2: Explains existing regex patterns
public class RegexExplainerAgent : AIOptions {
    public override string GetSystemPrompt() =>
        "You are a regex expert. Explain how regex patterns work. " +
        "Be concise - a few sentences for simple patterns, a paragraph for complex ones. " +
        "Don't give examples unless asked.";

    public override string HintForUserInput => "Provide a regex pattern to explain...";

    public override string FormatUserQuestion(string userQuestion) =>
        $"Please explain this regex:\n```regex\n{userQuestion}\n```";

    // This agent doesn't need reference text
    public override REFERENCE_TEXT_REPLACE_ACTION ReplaceAction =>
        REFERENCE_TEXT_REPLACE_ACTION.ReferenceTextDisabled;
}
```

Add a ComboBox above the AI pane to switch agents:

```xml
<Grid>
    <Grid.RowDefinitions>
        <RowDefinition Height="Auto"/>
        <RowDefinition/>
    </Grid.RowDefinitions>

    <ComboBox x:Name="agentCombo" SelectionChanged="AgentCombo_SelectionChanged">
        <ComboBoxItem Content="Regex Generator" IsSelected="True"/>
        <ComboBoxItem Content="Regex Explainer"/>
    </ComboBox>

    <pia:UCAIExpandable Grid.Row="1" x:Name="ucAi"
                        TargetColumn="{Binding ElementName=AiColumn}"/>
</Grid>
```

Handle the switch in code-behind:

```csharp
private RegexGeneratorAgent _generatorAgent = new();
private RegexExplainerAgent _explainerAgent = new();

private void Window_Loaded(object sender, RoutedEventArgs e) {
    ucAi.AiControl.Configure(_generatorAgent);
    ucAi.AiControl.ImportData(LoadSavedSettings());
}

private void AgentCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) {
    if (!IsLoaded) return;

    AIOptions newAgent = agentCombo.SelectedIndex == 0 ? _generatorAgent : _explainerAgent;
    ucAi.Configure(newAgent);  // Clears chat, keeps provider settings
}
```

You can also trigger agent switches programmatically and send a message:

```csharp
private async void ExplainRegex_Click(object sender, RoutedEventArgs e) {
    agentCombo.SelectedIndex = 1;  // Switch to explainer agent
    await ucAi.SendMessage($"`{txtRegex.Text}`");  // Send the current pattern
}
```

## Developer / Technical Notes
The control uses Micrsoft's Microsoft.SemanticKernel AI backend to talk with different AI model providers.  Most providers can work with the standard OpenAI style protocol so can be added using the custom endpoint provider in our settings.  Additional providers that work through Microsoft.SemanticKernel can be added pretty easily, look at `AiModelProvider.cs` for examples.  `GithubCopilotProvider.cs` shows the most complex provider using a custom login flow and auto token discovery.
