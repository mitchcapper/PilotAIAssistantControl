# PilotAiAssistControlWPF

A WPF user control that provides an AI chat assistant interface with support for multiple AI providers (GitHub Copilot, OpenAI, and custom providers). Includes both a standalone chat control and an expandable sidebar version.  It has integrated support for discovery of local existing GitHub Copilot tokens (ie from vscode) and also integrates a login flow for users that don't already have it present.

You can optionally provide a "reference document" which can be a longer document the user is working on and allow the user to control how much(if any) of it to send along.


## TOC

<!-- TOC -->

- [PilotAiAssistControlWPF](#pilotaiassistcontrolwpf)
	- [Installation](#installation)
	- [Quick Start](#quick-start)
		- [Add Namespace](#add-namespace)
		- [Create Your Options Class](#create-your-options-class)
		- [Embed the Control](#embed-the-control)
		- [Initialize in Code-Behind](#initialize-in-code-behind)
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
	- [Requirements](#requirements)

<!-- /TOC -->


## Installation

```
Install-Package PilotAiAssistControlWPF
```

## Quick Start

### 1. Add Namespace

```xml
xmlns:pia="clr-namespace:PilotAIAssistantControl;assembly=PilotAIAssistantControl"
```

### 2. Create Your Options Class

Create a class inheriting from `AIOptions` to configure the AI behavior:

```csharp
public class RegexAIOptions : AIOptions {
    public override string GetSystemPrompt() =>
        "You are a C# Regex expert assistant. " +
        "Provide regex patterns inside Markdown code blocks (```regex ... ```). " +
        "Explain how the pattern works briefly.";
}
```

### 3. Embed the Control

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

### 4. Initialize in Code-Behind

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
| `GetSystemPrompt()` | **Required.** Returns the system prompt that defines the AI's behavior and context. |
| `Providers` | Array of available AI providers. Defaults to built-in providers (GitHub Copilot, OpenAI, etc.). |
| `ReplaceAction` | Controls how reference text updates are handled in chat history. Default: `ChangeOldToPlaceholder`. |
| `GetCurrentReferenceText()` | Returns the current reference text to send with queries (e.g., document content). |
| `ReferenceTextHeader` | Header shown to the AI for reference text blocks. Default: `"Reference Text"`. |
| `ReferenceTextDisplayName` | User-facing name for reference text. Default: `"Reference Text"`. |
| `DefaultMaxReferenceTextCharsToSend` | Max characters of reference text to include. Default: `5000`. |
| `AllowUserToSetMaxReferenceTextCharsToSend` | Show UI to let user adjust max chars. Default: `true`. |
| `HintForUserInput` | Placeholder text for the chat input box. Default: `"Ask your question here..."`. |
| `FormatUserQuestion(string)` | Transform user questions before sending to AI. |
| `CodeblockActions` | Custom actions shown on code blocks in AI responses. |
| `HandleDebugMessage(string)` | Receive debug messages from the AI service. |

### Reference Text Replace Actions

When using reference text that changes during the conversation:

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

- `GenericCodeblockAction.ClipboardAction` - A pre-built "📋 Copy" action that copies the code to clipboard.

### Creating Custom Actions

Use `GenericCodeblockAction` to create custom actions:

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

To show actions only for specific code block types, set `IsVisibleDel`:

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
public interface CodeblockAction {
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

## Requirements

- .NET 6.0+ or .NET Framework 4.7.2+
- WPF application
- Newtonsoft.Json (for settings serialization)
