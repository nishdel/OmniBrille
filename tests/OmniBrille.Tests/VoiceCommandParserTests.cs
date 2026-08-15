using OmniBrille.Core;

namespace OmniBrille.Tests;

public sealed class VoiceCommandParserTests
{
    [Theory]
    [InlineData("Go back", VoiceIntentKind.GoBack)]
    [InlineData("previous!", VoiceIntentKind.GoBack)]
    [InlineData("ZOOM IN", VoiceIntentKind.ZoomIn)]
    [InlineData("zoom out.", VoiceIntentKind.ZoomOut)]
    [InlineData("reset the view", VoiceIntentKind.ResetView)]
    [InlineData("switch to structure", VoiceIntentKind.SwitchToStructure)]
    [InlineData("context mode", VoiceIntentKind.SwitchToContext)]
    [InlineData("show what is related to this", VoiceIntentKind.ShowRelatedToFocus)]
    [InlineData("use dark theme", VoiceIntentKind.UseDarkTheme)]
    [InlineData("light mode", VoiceIntentKind.UseLightTheme)]
    [InlineData("show details", VoiceIntentKind.OpenDetails)]
    [InlineData("hide details", VoiceIntentKind.CloseDetails)]
    [InlineData("show accessible list", VoiceIntentKind.ShowAccessibleList)]
    [InlineData("close list", VoiceIntentKind.HideAccessibleList)]
    [InlineData("clear search", VoiceIntentKind.ClearSearch)]
    [InlineData("cancel", VoiceIntentKind.Cancel)]
    public void Parse_RecognizesBoundedDeterministicCommands(string transcript, VoiceIntentKind expected)
    {
        var result = VoiceCommandParser.Parse(transcript);

        Assert.Equal(expected, result.Kind);
        Assert.Null(result.Argument);
    }

    [Theory]
    [InlineData("Open Documents", VoiceIntentKind.OpenVisibleNode, "Documents")]
    [InlineData("focus project files.", VoiceIntentKind.FocusVisibleNode, "project files")]
    [InlineData("Find the invoice from July", VoiceIntentKind.Search, "the invoice from July")]
    [InlineData("Search for Raspberry Pi monitoring", VoiceIntentKind.Search, "Raspberry Pi monitoring")]
    [InlineData("Show me climbing photos", VoiceIntentKind.Search, "climbing photos")]
    [InlineData("Files related to monitoring", VoiceIntentKind.Search, "Files related to monitoring")]
    public void Parse_PreservesArgumentsForVisibleNodeOrSearch(
        string transcript,
        VoiceIntentKind expected,
        string argument)
    {
        var result = VoiceCommandParser.Parse(transcript);

        Assert.Equal(expected, result.Kind);
        Assert.Equal(argument, result.Argument);
    }

    [Fact]
    public void NormalizeForComparison_RemovesPunctuationAndCollapsesWhitespace()
    {
        Assert.Equal("project files", VoiceCommandParser.NormalizeForComparison("  PROJECT,   Files?! "));
    }
}
